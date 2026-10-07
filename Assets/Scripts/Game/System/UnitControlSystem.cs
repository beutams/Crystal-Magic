using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(UnitAvoidanceSystem))]
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
            Scope = SystemAPI.TryGetSingleton(out BattleSimulationScope scope) ? scope : default,
            Players = SystemAPI.GetComponentLookup<PlayerInputComponent>(true),
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitControlTickJob : IJobEntity
{
    public float DeltaTime;
    public BattleSimulationScope Scope;
    [ReadOnly] public ComponentLookup<PlayerInputComponent> Players;

    private void Execute(Entity entity, ref UnitControlRuntimeComponent runtime)
    {
        if (Scope.Pass != BattleSimulationPass.All && !Scope.Includes(Players.HasComponent(entity)))
            return;
        UnitControlUtility.TickAndRefresh(ref runtime, DeltaTime);
    }
}
