using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
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
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(UnitDeathComponent), typeof(VfxArrivalComponent))]
    private partial struct UnitMoveJob : IJobEntity
    {
        public float DeltaTime;

        [ReadOnly]
        public ComponentLookup<UnitAvoidanceComponent> Avoidances;

        [ReadOnly]
        public ComponentLookup<UnitModifierComponent> Modifiers;

        private void Execute(
            Entity entity,
            ref UnitMoveComponent move,
            ref UnitFacingComponent facing,
            ref PhysicsVelocity physicsVelocity,
            ref LocalTransform transform)
        {
            float2 requestedDirection = move.Direction;
            float requestedMoveSpeed = move.CommandMoveSpeed;
            bool hasFrameVelocity = move.HasFrameVelocity != 0;
            float2 frameVelocity = move.FrameVelocity;
            UnitMoveComponent oldMove = move;
            UnitFacingComponent oldFacing = facing;
            LocalTransform oldTransform = transform;

            if (hasFrameVelocity)
            {
                move.Velocity = frameVelocity;
            }
            else if (Avoidances.TryGetComponent(entity, out UnitAvoidanceComponent avoidance) &&
                     avoidance.HasResolvedVelocity != 0)
            {
                move.Velocity = avoidance.ResolvedVelocity;
                float2 resolvedDirection = math.normalizesafe(move.Velocity, float2.zero);
                if (math.lengthsq(resolvedDirection) > 0.0001f)
                    facing.Direction = resolvedDirection;
            }
            else
            {
                float2 targetDirection = math.normalizesafe(requestedDirection, float2.zero);
                if (math.lengthsq(targetDirection) > 0.0001f)
                    facing.Direction = targetDirection;

                UnitModifierComponent modifier = Modifiers.TryGetComponent(
                    entity,
                    out UnitModifierComponent resolvedModifier)
                    ? resolvedModifier
                    : UnitModifierComponent.CreateIdentity();
                float resolvedSpeed = requestedMoveSpeed >= 0f
                    ? requestedMoveSpeed
                    : UnitModifierResolver.GetMoveSpeed(in move, in modifier);
                float targetSpeed = resolvedSpeed * move.StateMoveMultiplier;
                float maxSpeed = math.abs(targetSpeed);
                float maxAcceleration = math.max(
                    0f,
                    UnitModifierResolver.GetMaxAcceleration(in move, in modifier));
                float2 targetVelocity = targetDirection * targetSpeed;
                if (move.StateMoveMultiplier <= 0f)
                    move.Velocity = float2.zero;
                else
                    UpdateMoveVelocity(ref move, targetVelocity, maxAcceleration, maxSpeed, DeltaTime);
            }

            ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);

            if (!move.Velocity.Equals(oldMove.Velocity) ||
                math.lengthsq(move.Velocity) > 0.0001f ||
                !math.all(transform.Position == oldTransform.Position))
            {
                move.NetworkDirty = 1;
            }

            if (!facing.Direction.Equals(oldFacing.Direction))
                facing.NetworkDirty = 1;

            move.Direction = float2.zero;
            move.CommandMoveSpeed = -1f;
            move.FrameVelocity = float2.zero;
            move.HasFrameVelocity = 0;
        }

        private static void UpdateMoveVelocity(
            ref UnitMoveComponent move,
            float2 targetVelocity,
            float maxAcceleration,
            float maxSpeed,
            float deltaTime)
        {
            float2 difference = targetVelocity - move.Velocity;
            float differenceLength = math.length(difference);

            if (differenceLength > 0.0001f)
            {
                float step = maxAcceleration * deltaTime;
                if (step >= differenceLength)
                    move.Velocity = targetVelocity;
                else
                    move.Velocity += difference / differenceLength * step;
            }

            float velocityLength = math.length(move.Velocity);
            if (velocityLength > maxSpeed && velocityLength > 0.0001f)
                move.Velocity = move.Velocity / velocityLength * maxSpeed;
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
