using Unity.Entities;
using UnityEngine;

public sealed class UnitDeathAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitDeathAuthoring>
    {
        public override void Bake(UnitDeathAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UnitDeathComponent>(entity);
            SetComponentEnabled<UnitDeathComponent>(entity, false);
        }
    }
}

public struct UnitDeathComponent : IComponentData, IEnableableComponent
{
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(UnitDeathComponent), typeof(UnitDeathAuthoring))]
public static class UnitDeathSource
{
    [UnitSourceGet(0, "unit.death.isActive", UnitValueCategory.Bool)]
    public static bool TryGet(
        int operation,
        Entity entity,
        in ComponentLookup<UnitDeathComponent> deaths,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        if (operation != 0 || !deaths.HasComponent(entity))
        {
            result = default;
            return false;
        }

        result = UnitSourceValue.FromBool(deaths.IsComponentEnabled(entity));
        return true;
    }
}
