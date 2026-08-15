using Unity.Mathematics;
using Unity.Transforms;

[UnitSourceAuthoring(typeof(UnityEngine.Transform))]
public sealed class UnitTransformSource : UnitComponentSource<LocalTransform>
{
    protected override void Define(UnitSourceDefinitionBuilder<LocalTransform> builder)
    {
        builder.AddGet("unit.transform.position", UnitValueCategory.Float3,
            (in LocalTransform component) => UnitValue.FromFloat3(component.Position));
        builder.AddGet("unit.transform.forward", UnitValueCategory.Float3,
            (in LocalTransform component) => UnitValue.FromFloat3(math.mul(component.Rotation, new float3(0f, 0f, 1f))));
        builder.AddGet("unit.transform.scale", UnitValueCategory.Number,
            (in LocalTransform component) => UnitValue.FromFloat(component.Scale));
    }
}
