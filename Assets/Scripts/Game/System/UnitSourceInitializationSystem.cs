using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(BehaviorTreeInitSystem))]
public partial class UnitSourceInitializationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityQuery pendingQuery = SystemAPI.QueryBuilder()
            .WithAll<UnitFactionComponent>()
            .WithNone<UnitSourceRuntimeComponent>()
            .Build();

        using NativeArray<Entity> entities = pendingQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            UnitSourceAccessTable table = new();
            UnitComponentSourceRegistry.BindAll(
                new UnitSourceBindingContext(entity, EntityManager),
                table);

            EntityManager.AddComponentObject(entity, new UnitSourceRuntimeComponent
            {
                Table = table,
            });
        }
    }
}
