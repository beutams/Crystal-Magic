using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitQueryBuildSystem))]
public partial struct UnitInteractableFactionInitializationSystem : ISystem
{
    private EntityQuery _missingFactionQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _missingFactionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitInteractableComponent, UnitInitializationPendingTag>()
            .WithNone<UnitFactionComponent>()
            .Build(ref state);
        state.RequireForUpdate(_missingFactionQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commands = new(Allocator.TempJob);
        state.Dependency = new UnitInteractableFactionInitializationJob
        {
            Commands = commands.AsParallelWriter(),
        }.ScheduleParallel(_missingFactionQuery, state.Dependency);
        state.Dependency.Complete();
        commands.Playback(state.EntityManager);
        commands.Dispose();
    }
}

[BurstCompile]
public partial struct UnitInteractableFactionInitializationJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Commands;

    private void Execute([EntityIndexInQuery] int index, Entity entity)
    {
        Commands.AddComponent(index, entity, new UnitFactionComponent
        {
            Value = UnitFactionType.Interactable,
        });
    }
}
