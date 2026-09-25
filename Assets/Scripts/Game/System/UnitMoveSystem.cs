using Unity.Burst;
using Unity.Collections;
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
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitMoveComponent>();
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
            PlayerInputs = SystemAPI.GetComponentLookup<PlayerInputComponent>(true),
            BattlePlayerStatuses = SystemAPI.GetComponentLookup<BattlePlayerStatusComponent>(true),
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(VfxArrivalComponent))]
    private partial struct UnitMoveJob : IJobEntity
    {
        public float DeltaTime;

        [ReadOnly]
        public ComponentLookup<UnitAvoidanceComponent> Avoidances;

        [ReadOnly]
        public ComponentLookup<UnitModifierComponent> Modifiers;

        [ReadOnly]
        public ComponentLookup<UnitDeathComponent> Deaths;

        [ReadOnly]
        public ComponentLookup<PlayerInputComponent> PlayerInputs;

        [ReadOnly]
        public ComponentLookup<BattlePlayerStatusComponent> BattlePlayerStatuses;

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
            UnitMoveComponent oldMove = move;
            bool isDead = Deaths.HasComponent(entity) && Deaths.IsComponentEnabled(entity);
            bool isBattleSpectator = BattlePlayerStatuses.TryGetComponent(
                entity, out BattlePlayerStatusComponent status) && status.IsSpectator;
            bool isWaitingForTransition = status.IsWaitingForTransition;

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
                // 观战不参与物理碰撞，也不受死亡时遗留的控制/施法移动倍率影响。
                transform.Position += new float3(move.Velocity, 0f) * DeltaTime;
                physicsVelocity = default;
                hasFrameVelocity = false;
            }
            else if (isDead)
            {
                move.Velocity = float2.zero;
            }
            else if (hasFrameVelocity)
            {
                move.Velocity = frameVelocity;
            }
            else if (!isDead && Avoidances.TryGetComponent(entity, out UnitAvoidanceComponent avoidance) &&
                     avoidance.HasResolvedVelocity != 0)
            {
                move.Velocity = avoidance.ResolvedVelocity;
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
                UnitMoveSimulationUtility.ResolveDesiredVelocity(ref move, in modifier, DeltaTime);
            }

            if (!isBattleSpectator && !isWaitingForTransition)
                ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);

            if (!move.Velocity.Equals(oldMove.Velocity) ||
                math.lengthsq(move.Velocity) > 0.0001f ||
                !math.all(transform.Position == move.LastObservedPosition))
            {
                move.NetworkDirty = 1;
            }

            move.LastObservedPosition = transform.Position;

            UnitMoveSimulationUtility.ClearFrameCommands(ref move);
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
