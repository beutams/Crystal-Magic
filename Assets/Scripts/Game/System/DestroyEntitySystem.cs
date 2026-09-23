using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup), OrderLast = true)]
partial struct DestroyEntitySystem : ISystem
{
    private EntityQuery _destroyQuery;

    public void OnCreate(ref SystemState state)
    {
        _destroyQuery = state.GetEntityQuery(ComponentType.ReadOnly<DestroyEntityFlag>());
    }

    public void OnUpdate(ref SystemState state)
    {
        using NativeArray<Entity> entities = _destroyQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            if (state.EntityManager.Exists(entities[index]))
                state.EntityManager.DestroyEntity(entities[index]);
        }
    }
}
