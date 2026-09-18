using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class UnitFacingAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitFacingAuthoring>
    {
        public override void Bake(UnitFacingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitFacingComponent
            {
                Direction = new float2(1f, 0f),
            });
        }
    }
}

public struct UnitFacingComponent : IComponentData
{
    public float2 Direction;
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(UnitFacingComponent), typeof(UnitFacingAuthoring))]
public static class UnitFacingSource
{
    [UnitSourceGet(0, "unit.facing.direction", UnitValueCategory.Float2)]
    public static bool TryGet(
        int operation,
        in UnitFacingComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation == 0 ? UnitSourceValue.FromFloat2(value.Direction) : UnitSourceValue.None;
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.facing.setDirection", UnitValueCategory.Float2, ParameterNames = new[] { "Direction" })]
    public static bool TrySet(int operation, ref UnitFacingComponent value, in UnitSourceArguments arguments)
    {
        if (operation != 0 || !arguments.TryGetFloat2(0, out float2 direction))
            return false;

        value.Direction = math.normalizesafe(direction, value.Direction);
        value.NetworkDirty = 1;
        return true;
    }
}
