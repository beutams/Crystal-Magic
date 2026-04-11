using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// 单位移动系统——从加速度意图计算速度，驱动 PhysicsVelocity。
///
/// 每帧逻辑：
///   targetVelocity = AccelInput * RealMoveSpeed
///   Velocity 向 targetVelocity 以 RealMaxAcceleration * dt 逼近
///   PhysicsVelocity.Linear = (Velocity.x, Velocity.y, 0)
///
/// AccelInput = float2.zero 时自然减速到停。
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(UnitStateTransitionSystem))]
partial struct UnitMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        new UnitMoveJob { DeltaTime = dt }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct UnitMoveJob : IJobEntity
{
    public float DeltaTime;

    public void Execute(ref UnitMoveComponent move, ref PhysicsVelocity physicsVelocity)
    {
        float maxSpeed = move.RealMoveSpeed;
        float maxAccel = move.RealMaxAcceleration;

        float2 targetVel = move.AccelInput * maxSpeed;

        float2 diff    = targetVel - move.Velocity;
        float  diffLen = math.length(diff);

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

        physicsVelocity.Linear  = new float3(move.Velocity.x, move.Velocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
    }
}
