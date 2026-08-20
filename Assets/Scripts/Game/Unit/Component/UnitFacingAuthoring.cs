using Unity.Entities;
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
    }
}
