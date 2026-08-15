using Unity.Entities;
using Unity.Mathematics;

public struct UnitFacingComponent : IComponentData
{
    public float2 Direction;
}

[UnitSourceAuthoring(typeof(UnitMoveAuthoring))]
public sealed class UnitFacingSource : UnitComponentSource<UnitFacingComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitFacingComponent> builder)
    {
        builder.AddGet("unit.facing.direction", UnitValueCategory.Float2,
            (in UnitFacingComponent value) => UnitValue.FromFloat2(value.Direction));
        builder.AddSet("unit.facing.direction", UnitValueCategory.Float2,
            (ref UnitFacingComponent value, UnitValue input) =>
            {
                value.Direction = math.normalizesafe(input.Float2, value.Direction);
                return true;
            });
    }
}
