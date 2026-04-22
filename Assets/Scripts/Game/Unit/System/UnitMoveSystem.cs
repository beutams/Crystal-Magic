using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// 鍗曚綅绉诲姩绯荤粺鈥斺€斾粠鍔犻€熷害鎰忓浘璁＄畻閫熷害锛岄┍鍔?PhysicsVelocity銆?///
/// 姣忓抚閫昏緫锛?///   targetVelocity = AccelInput * RealMoveSpeed
///   Velocity 鍚?targetVelocity 浠?RealMaxAcceleration * dt 閫艰繎
///   PhysicsVelocity.Linear = (Velocity.x, Velocity.y, 0)
///
/// AccelInput = float2.zero 鏃惰嚜鐒跺噺閫熷埌鍋溿€?/// </summary>
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
