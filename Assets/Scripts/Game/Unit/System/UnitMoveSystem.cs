using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitStateTransitionSystem))]
partial struct UnitMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        ComponentLookup<LocalToWorld> transformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        new UnitMoveJob
        {
            DeltaTime = dt,
            TransformLookup = transformLookup,
        }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct UnitMoveJob : IJobEntity
{
    [ReadOnly]
    public ComponentLookup<LocalToWorld> TransformLookup;

    public float DeltaTime;

    public void Execute(
        ref UnitMoveComponent move,
        ref PhysicsVelocity physicsVelocity,
        ref LocalTransform transform,
        ref UnitKnockbackComponent knockback,
        in UnitControlStateComponent controlState)
    {
        switch (controlState.ActiveType)
        {
            case UnitControlType.Knockback:
                ApplyKnockback(ref move, ref physicsVelocity, ref transform, ref knockback);
                return;

            case UnitControlType.Fear:
                ApplyDirectedMovement(ref move, ref physicsVelocity, ref transform, GetFearDirection(transform.Position.xy, controlState.ActiveSourceEntity));
                return;

            case UnitControlType.Stun:
                ApplyStoppedMovement(ref move, ref physicsVelocity, ref transform);
                return;
        }

        float2 targetVel = move.AccelInput * move.RealMoveSpeed;
        UpdateMoveVelocity(ref move, targetVel);
        ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);
    }

    private void ApplyKnockback(ref UnitMoveComponent move, ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, ref UnitKnockbackComponent knockback)
    {
        move.AccelInput = float2.zero;
        move.Velocity = float2.zero;

        float2 currentVelocity = knockback.Velocity;
        float currentSpeed = math.length(currentVelocity);
        if (currentSpeed > 0.0001f)
        {
            float decelStep = math.max(0f, knockback.Damping) * DeltaTime;
            if (decelStep >= currentSpeed)
                knockback.Velocity = float2.zero;
            else
                knockback.Velocity = currentVelocity - (currentVelocity / currentSpeed) * decelStep;
        }
        else
        {
            knockback.Velocity = float2.zero;
        }

        ApplyPlanarTransform(ref physicsVelocity, ref transform, currentVelocity);
    }

    private void ApplyDirectedMovement(ref UnitMoveComponent move, ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, float2 direction)
    {
        move.AccelInput = float2.zero;
        float2 targetVel = direction * move.RealMoveSpeed;
        UpdateMoveVelocity(ref move, targetVel);
        ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);
    }

    private void ApplyStoppedMovement(ref UnitMoveComponent move, ref PhysicsVelocity physicsVelocity, ref LocalTransform transform)
    {
        move.AccelInput = float2.zero;
        move.Velocity = float2.zero;
        ApplyPlanarTransform(ref physicsVelocity, ref transform, float2.zero);
    }

    private void UpdateMoveVelocity(ref UnitMoveComponent move, float2 targetVel)
    {
        float2 diff = targetVel - move.Velocity;
        float diffLen = math.length(diff);
        float maxAccel = move.RealMaxAcceleration;

        if (diffLen > 0.0001f)
        {
            float step = maxAccel * DeltaTime;
            if (step >= diffLen)
                move.Velocity = targetVel;
            else
                move.Velocity += (diff / diffLen) * step;
        }

        float maxSpeed = move.RealMoveSpeed;
        float velLen = math.length(move.Velocity);
        if (velLen > maxSpeed && velLen > 0.0001f)
            move.Velocity = (move.Velocity / velLen) * maxSpeed;
    }

    private float2 GetFearDirection(float2 selfPosition, Entity sourceEntity)
    {
        if (sourceEntity == Entity.Null || !TransformLookup.HasComponent(sourceEntity))
            return float2.zero;

        float2 direction = selfPosition - TransformLookup[sourceEntity].Position.xy;
        return math.normalizesafe(direction);
    }

    private static void ApplyPlanarTransform(ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, float2 planarVelocity)
    {
        physicsVelocity.Linear = new float3(planarVelocity.x, planarVelocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
        transform.Position.z = 0f;
        transform.Rotation = quaternion.identity;
    }
}
