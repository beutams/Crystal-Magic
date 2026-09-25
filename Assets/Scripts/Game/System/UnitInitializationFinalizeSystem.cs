using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(BehaviorTreeInitSystem))]
[UpdateAfter(typeof(StateScriptInitSystem))]
public partial struct UnitInitializationFinalizeSystem : ISystem
{
    private EntityQuery _pendingInitializationQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _pendingInitializationQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitInitializationPendingTag>()
            .Build(ref state);
        state.RequireForUpdate(_pendingInitializationQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.RemoveComponent<UnitInitializationPendingTag>(_pendingInitializationQuery);
    }
}
