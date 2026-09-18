using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(StateScriptInitSystem))]
public partial class UnitAnimationStateInitializationSystem : SystemBase
{
    private EntityQuery _missingStateQuery;

    protected override void OnCreate()
    {
        _missingStateQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<UnitAnimationComponent>() },
            None = new[] { ComponentType.ReadOnly<UnitAnimationStateComponent>() },
        });
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _missingStateQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            UnitAnimationComponent animation = EntityManager.GetComponentObject<UnitAnimationComponent>(entity);
            EntityManager.AddComponentData(entity, new UnitAnimationStateComponent
            {
                AnimationName = animation?.CurrentAnimationName ?? default,
                Sequence = animation?.RequestedSequence ?? 0u,
                NetworkDirty = 0,
            });
        }
    }
}
