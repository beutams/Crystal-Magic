using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
partial struct UnitMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        new UnitMoveJob
        {
            DeltaTime = dt,
        }.ScheduleParallel();
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitMoveJob : IJobEntity
{
    public float DeltaTime;

    public void Execute(
        ref UnitMoveComponent move,
        ref PhysicsVelocity physicsVelocity,
        ref LocalTransform transform)
    {
        float2 targetDirection = math.normalizesafe(move.DesiredDirection, float2.zero);
        float maxSpeed = math.max(0f, move.DesiredMaxSpeed);
        float maxAcceleration = math.max(0f, move.DesiredAcceleration);
        float2 targetVel = targetDirection * maxSpeed;
        UpdateMoveVelocity(ref move, targetVel, maxAcceleration, maxSpeed);
        ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);
    }

    private void UpdateMoveVelocity(ref UnitMoveComponent move, float2 targetVel, float maxAccel, float maxSpeed)
    {
        float2 diff = targetVel - move.Velocity;
        float diffLen = math.length(diff);

        if (diffLen > 0.0001f)
        {
            float step = maxAccel * DeltaTime;
            if (step >= diffLen)
                move.Velocity = targetVel;
            else
                move.Velocity += (diff / diffLen) * step;
        }

        float velLen = math.length(move.Velocity);
        if (velLen > maxSpeed && velLen > 0.0001f)
            move.Velocity = (move.Velocity / velLen) * maxSpeed;
    }

    private static void ApplyPlanarTransform(ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, float2 planarVelocity)
    {
        physicsVelocity.Linear = new float3(planarVelocity.x, planarVelocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
        transform.Position.z = 0f;
        transform.Rotation = quaternion.identity;
    }
}
