using Unity.Burst;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(SkillReleaseSystem))]
[BurstCompile]
partial struct UnitControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitControlRuntimeComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new UnitControlTickJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitControlTickJob : IJobEntity
{
    public float DeltaTime;

    private void Execute(ref UnitControlRuntimeComponent runtime)
    {
        UnitControlUtility.TickAndRefresh(ref runtime, DeltaTime);
    }
}
