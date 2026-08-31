using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
partial class UnitMoveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<UnitMoveComponent> moveRef,
                  RefRW<UnitFacingComponent> facingRef,
                  RefRW<PhysicsVelocity> physicsVelocityRef,
                  RefRW<LocalTransform> transformRef,
                  Entity entity) in
                 SystemAPI.Query<RefRW<UnitMoveComponent>, RefRW<UnitFacingComponent>, RefRW<PhysicsVelocity>, RefRW<LocalTransform>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            UnitMoveComponent move = moveRef.ValueRO;
            UnitFacingComponent facing = facingRef.ValueRO;
            float2 targetDirection = math.normalizesafe(move.Direction, float2.zero);
            if (math.lengthsq(targetDirection) > 0.0001f)
                facing.Direction = targetDirection;

            float targetSpeed = UnitModifierResolver.GetMoveSpeed(EntityManager, entity) * move.StateMoveMultiplier;
            float maxSpeed = math.abs(targetSpeed);
            float maxAcceleration = math.max(0f, UnitModifierResolver.GetMaxAcceleration(EntityManager, entity));
            float2 targetVelocity = targetDirection * targetSpeed;
            if (move.StateMoveMultiplier <= 0f)
                move.Velocity = float2.zero;
            else
                UpdateMoveVelocity(ref move, targetVelocity, maxAcceleration, maxSpeed, deltaTime);

            PhysicsVelocity physicsVelocity = physicsVelocityRef.ValueRO;
            LocalTransform transform = transformRef.ValueRO;
            ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);

            moveRef.ValueRW = move;
            facingRef.ValueRW = facing;
            physicsVelocityRef.ValueRW = physicsVelocity;
            transformRef.ValueRW = transform;
        }
    }

    private static void UpdateMoveVelocity(ref UnitMoveComponent move, float2 targetVelocity, float maxAcceleration, float maxSpeed, float deltaTime)
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

    private static void ApplyPlanarTransform(ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, float2 planarVelocity)
    {
        physicsVelocity.Linear = new float3(planarVelocity.x, planarVelocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
        transform.Position.z = 0f;
        transform.Rotation = quaternion.identity;
    }
}
