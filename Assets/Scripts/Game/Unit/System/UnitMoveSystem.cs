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

