using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientPresentationSystemGroup), OrderLast = true)]
public partial class ClientDestroyEntitySystem : SystemBase
{
    private EntityQuery _destroyQuery;

    protected override void OnCreate()
    {
        _destroyQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<DestroyEntityFlag>() },
            Options = EntityQueryOptions.IgnoreComponentEnabledState,
        });
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _destroyQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (EntityManager.Exists(entity) && EntityManager.IsComponentEnabled<DestroyEntityFlag>(entity))
                EntityManager.DestroyEntity(entity);
        }
    }
}
