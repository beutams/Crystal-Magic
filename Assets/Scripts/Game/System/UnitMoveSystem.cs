using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitAvoidanceSystem))]
[UpdateBefore(typeof(VfxArrivalSystem))]
partial struct UnitMoveSystem : ISystem
{
    private EntityQuery _navigationMapQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitMoveComponent>();
        _navigationMapQuery = SystemAPI.QueryBuilder()
            .WithAll<DungeonNavigationMapComponent, DungeonNavigationCollisionWord>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        UnitMoveJob job = new()
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Avoidances = SystemAPI.GetComponentLookup<UnitAvoidanceComponent>(true),
            Modifiers = SystemAPI.GetComponentLookup<UnitModifierComponent>(true),
            Deaths = SystemAPI.GetComponentLookup<UnitDeathComponent>(true),
            Controls = SystemAPI.GetComponentLookup<UnitControlRuntimeComponent>(true),
            PlayerInputs = SystemAPI.GetComponentLookup<PlayerInputComponent>(true),
            BattlePlayerStatuses = SystemAPI.GetComponentLookup<BattlePlayerStatusComponent>(true),
            Facings = SystemAPI.GetComponentLookup<UnitFacingComponent>(),
            NavigationMapEntity = _navigationMapQuery.CalculateEntityCount() == 1
                ? _navigationMapQuery.GetSingletonEntity() : Entity.Null,
            CollisionWords = SystemAPI.GetBufferLookup<DungeonNavigationCollisionWord>(true),
        };
        if (job.NavigationMapEntity != Entity.Null)
            job.NavigationMap = _navigationMapQuery.GetSingleton<DungeonNavigationMapComponent>();
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(VfxArrivalComponent))]
    private partial struct UnitMoveJob : IJobEntity
    {
        public float DeltaTime;
        public Entity NavigationMapEntity;
        public DungeonNavigationMapComponent NavigationMap;

        [ReadOnly]
        public BufferLookup<DungeonNavigationCollisionWord> CollisionWords;

        [ReadOnly]
        public ComponentLookup<UnitAvoidanceComponent> Avoidances;

        [ReadOnly]
        public ComponentLookup<UnitModifierComponent> Modifiers;

        [ReadOnly]
        public ComponentLookup<UnitDeathComponent> Deaths;

        [ReadOnly]
        public ComponentLookup<UnitControlRuntimeComponent> Controls;

        [ReadOnly]
        public ComponentLookup<PlayerInputComponent> PlayerInputs;

        [ReadOnly]
        public ComponentLookup<BattlePlayerStatusComponent> BattlePlayerStatuses;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<UnitFacingComponent> Facings;

        private void Execute(
            Entity entity,
            ref UnitMoveComponent move,
            ref PhysicsVelocity physicsVelocity,
            ref LocalTransform transform)
        {
            float2 requestedDirection = move.Direction;
            float requestedMoveSpeed = move.CommandMoveSpeed;
            bool hasFrameVelocity = move.HasFrameVelocity != 0;
            float2 frameVelocity = move.FrameVelocity;
            float2 desiredFacingDirection = float2.zero;
            UnitMoveComponent oldMove = move;
            bool isDead = Deaths.HasComponent(entity) && Deaths.IsComponentEnabled(entity);
            bool isBattleSpectator = BattlePlayerStatuses.TryGetComponent(
                entity, out BattlePlayerStatusComponent status) && status.IsSpectator;
            bool isWaitingForTransition = status.IsWaitingForTransition;
            bool hasTeleportRequest = move.HasTeleportRequest != 0;
            bool teleportApplied = false;

            if (isWaitingForTransition)
            {
                move.Velocity = float2.zero;
                physicsVelocity = default;
                hasFrameVelocity = false;
            }
            else if (isBattleSpectator)
            {
                requestedDirection = status.ConnectionState == BattlePlayerConnectionState.Online &&
                                     PlayerInputs.TryGetComponent(entity, out PlayerInputComponent input)
                    ? input.Move : float2.zero;
                UnitModifierComponent identity = UnitModifierComponent.CreateIdentity();
                move.Velocity = math.normalizesafe(requestedDirection) *
                                UnitModifierResolver.GetMoveSpeed(in move, in identity);
                desiredFacingDirection = requestedDirection;
                // 观战不参与物理碰撞，也不受死亡时遗留的控制/施法移动倍率影响。
                transform.Position += new float3(move.Velocity, 0f) * DeltaTime;
                physicsVelocity = default;
                hasFrameVelocity = false;
            }
            else if (isDead)
            {
                move.Velocity = float2.zero;
            }
            else if (Controls.TryGetComponent(entity, out UnitControlRuntimeComponent control) &&
                     control.LockMove != 0)
            {
                // A movement lock owns the final velocity. Stun supplies zero
                // velocity while knockback supplies its decaying control velocity.
                move.Velocity = control.ActiveMotionVelocity;
                desiredFacingDirection = move.Velocity;
                move.Direction = float2.zero;
                move.FrameVelocity = float2.zero;
                move.HasFrameVelocity = 0;
                move.CommandMoveSpeed = -1f;
            }
            else if (hasTeleportRequest)
            {
                float3 destination = move.TeleportDestination;
                bool valid = move.ValidateTeleportPosition == 0 ||
                    (NavigationMapEntity != Entity.Null &&
                     SpawnPositionUtility.TryFindNearby(in NavigationMap, CollisionWords[NavigationMapEntity].AsNativeArray(),
                         destination, move.TeleportClearanceRadius, move.TeleportSearchRadius, out destination));
                if (valid)
                {
                    teleportApplied = true;
                    float2 previousPosition = transform.Position.xy;
                    transform.Position = new float3(destination.xy, 0f);
                    if (move.ValidateTeleportPosition == 0)
                        desiredFacingDirection = math.normalizesafe(transform.Position.xy - previousPosition, float2.zero);
                    move.PredictedPosition = transform.Position;
                    move.HasPredictedPosition = 1;
                }
                move.Velocity = float2.zero;
                move.Direction = float2.zero;
                move.FrameVelocity = float2.zero;
                move.HasFrameVelocity = 0;
                move.CommandMoveSpeed = -1f;
                physicsVelocity = default;
            }
            else if (hasFrameVelocity)
            {
                move.Velocity = frameVelocity;
            }
            else if (!isDead && Avoidances.TryGetComponent(entity, out UnitAvoidanceComponent avoidance) &&
                     avoidance.HasResolvedVelocity != 0)
            {
                move.Velocity = avoidance.ResolvedVelocity;
                desiredFacingDirection = move.Velocity;
            }
            else
            {
                UnitModifierComponent modifier = Modifiers.TryGetComponent(
                    entity,
                    out UnitModifierComponent resolvedModifier)
                    ? resolvedModifier
                    : UnitModifierComponent.CreateIdentity();
                move.Direction = requestedDirection;
                move.CommandMoveSpeed = requestedMoveSpeed;
                desiredFacingDirection = requestedDirection;
                UnitMoveSimulationUtility.ResolveDesiredVelocity(ref move, in modifier, DeltaTime);
            }

            UpdateFacing(entity, desiredFacingDirection);

            if (!isBattleSpectator && !isWaitingForTransition && !teleportApplied)
                ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);

            if (!move.Velocity.Equals(oldMove.Velocity) ||
                math.lengthsq(move.Velocity) > 0.0001f ||
                !math.all(transform.Position == move.LastObservedPosition))
            {
                move.NetworkDirty = 1;
            }

            move.LastObservedPosition = transform.Position;
            move.HasTeleportRequest = 0;
            move.ValidateTeleportPosition = 0;

            UnitMoveSimulationUtility.ClearFrameCommands(ref move);
        }

        private void UpdateFacing(Entity entity, float2 desiredDirection)
        {
            if (math.lengthsq(desiredDirection) <= 0.0001f ||
                !Facings.TryGetComponent(entity, out UnitFacingComponent facing))
            {
                return;
            }

            float2 direction = math.normalize(desiredDirection);
            if (math.all(facing.Direction == direction))
                return;

            facing.Direction = direction;
            facing.NetworkDirty = 1;
            Facings[entity] = facing;
        }

        private static void ApplyPlanarTransform(
            ref PhysicsVelocity physicsVelocity,
            ref LocalTransform transform,
            float2 planarVelocity)
        {
            physicsVelocity.Linear = new float3(planarVelocity.x, planarVelocity.y, 0f);
            transform.Position.z = 0f;
        }
    }
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
[UpdateBefore(typeof(EffectExecutionSystem))]
partial struct VfxArrivalSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        VfxArrivalJob job = new()
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    private partial struct VfxArrivalJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(
            ref VfxArrivalComponent arrival,
            ref UnitMoveComponent move,
            ref LocalTransform transform,
            DynamicBuffer<EffectEntry> effects,
            EnabledRefRW<DestroyEntityFlag> destroyFlag)
        {
            arrival.Elapsed += DeltaTime;
            float progress = arrival.Duration <= 0f
                ? 1f
                : math.saturate(arrival.Elapsed / arrival.Duration);
            transform.Position = math.lerp(arrival.StartPosition, arrival.EndPosition, progress);
            move.Direction = float2.zero;
            move.CommandMoveSpeed = -1f;
            move.FrameVelocity = float2.zero;
            move.HasFrameVelocity = 0;

            if (progress < 1f)
                return;

            EffectRequestContext context = arrival.ArrivalContext;
            context.HasPosition = 1;
            context.Position = arrival.EndPosition;
            effects.Add(new EffectEntry
            {
                EffectListId = arrival.OnArrivalEffectListId,
                Context = context,
                RepeatCount = 1,
                ReleaseEffectListAfterExecution = arrival.OnArrivalEffectListId.IsValid ? (byte)1 : (byte)0,
                ReleaseManagedContextAfterExecution = arrival.ReleaseManagedContextAfterExecution,
            });
            destroyFlag.ValueRW = true;
        }
    }
}
