using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
partial struct VfxLifetimeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VfxLifetimeComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new VfxLifetimeJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct VfxLifetimeJob : IJobEntity
{
    public float DeltaTime;

    private void Execute(
        ref VfxLifetimeComponent lifetime,
        EnabledRefRW<DestroyEntityFlag> destroyFlag)
    {
        if (destroyFlag.ValueRO)
            return;

        lifetime.RemainingSeconds -= DeltaTime;
        if (lifetime.RemainingSeconds <= 0f)
            destroyFlag.ValueRW = true;
    }
}
