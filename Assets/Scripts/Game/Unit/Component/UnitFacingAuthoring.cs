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
}

[UnitSourceAuthoring(typeof(UnitFacingAuthoring))]
public sealed class UnitFacingSource : UnitComponentSource<UnitFacingComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitFacingComponent> builder)
    {
        builder.AddGet("unit.facing.direction", UnitValueCategory.Float2,
            (in UnitFacingComponent value) => UnitValue.FromFloat2(value.Direction));
        builder.AddGet("unit.facing.x", UnitValueCategory.Number,
            (in UnitFacingComponent value) => UnitValue.FromFloat(value.Direction.x));
        builder.AddGet("unit.facing.y", UnitValueCategory.Number,
            (in UnitFacingComponent value) => UnitValue.FromFloat(value.Direction.y));
        builder.AddGet("unit.facing.angleDegrees", UnitValueCategory.Number,
            (in UnitFacingComponent value) => UnitValue.FromFloat(math.degrees(math.atan2(value.Direction.y, value.Direction.x))));
        builder.AddSet("unit.facing.direction", UnitValueCategory.Float2,
            (ref UnitFacingComponent value, UnitValue input) =>
            {
                value.Direction = math.normalizesafe(input.Float2, value.Direction);
                return true;
            });
    }
}
