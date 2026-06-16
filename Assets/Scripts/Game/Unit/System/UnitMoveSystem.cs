using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitSkillAnalysisSystem))]
[UpdateAfter(typeof(UnitStateMachineSystem))]
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
public partial struct UnitMoveJob : IJobEntity
{
    public float DeltaTime;

    public void Execute(
        ref UnitMoveComponent move,
        ref PhysicsVelocity physicsVelocity,
        ref LocalTransform transform)
    {
        switch (move.CommandType)
        {
            case UnitMoveCommandType.DirectVelocity:
                ApplyDirectVelocity(ref move, ref physicsVelocity, ref transform);
                return;
        }

        float2 commandDirection = move.CommandType == UnitMoveCommandType.Accelerate
            ? move.CommandDirection
            : float2.zero;
        float2 targetVel = commandDirection * move.RealMoveSpeed;
        UpdateMoveVelocity(ref move, targetVel);
        ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);
    }

    private void ApplyDirectVelocity(ref UnitMoveComponent move, ref PhysicsVelocity physicsVelocity, ref LocalTransform transform)
    {
        move.Velocity = move.DirectVelocity;
        ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);
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

    private static void ApplyPlanarTransform(ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, float2 planarVelocity)
    {
        physicsVelocity.Linear = new float3(planarVelocity.x, planarVelocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
        transform.Position.z = 0f;
        transform.Rotation = quaternion.identity;
    }
}
