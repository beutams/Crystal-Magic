using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(PlayerEquipmentPropertySystem))]
[UpdateBefore(typeof(UnitBuffSystem))]
partial class UnitSkillModifierResetSystem : SystemBase
{
    private EntityQuery _runtimeQuery;

    protected override void OnCreate()
    {
        _runtimeQuery = GetEntityQuery(ComponentType.ReadOnly<UnitSkillModifierRuntimeComponent>());
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _runtimeQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
            UnitSkillModifierUtility.ResetRuntimeModifiers(EntityManager, entities[i]);
    }
}
