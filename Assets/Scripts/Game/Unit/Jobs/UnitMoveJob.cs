using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

[BurstCompile]
public partial struct UnitMoveJob : IJobEntity
{
    public float DeltaTime;

    public void Execute(ref UnitMoveComponent move, ref PhysicsVelocity physicsVelocity)
    {
        float maxSpeed = move.RealMoveSpeed;
        float maxAccel = move.RealMaxAcceleration;

        float2 targetVel = move.AccelInput * maxSpeed;
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
        if (velLen > maxSpeed)
            move.Velocity = (move.Velocity / velLen) * maxSpeed;

        physicsVelocity.Linear = new float3(move.Velocity.x, move.Velocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
    }
}
