using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UnitSourceAuthoring(typeof(UnityEngine.Transform))]
public sealed class UnitTransformSource : UnitComponentSource<LocalTransform>
{
    private static readonly ComparatorParameterDefinition[] s_targetEntityParameter =
    {
        new("Target Unit", UnitValueCategory.Entity),
    };

    protected override void Define(UnitSourceDefinitionBuilder<LocalTransform> builder)
    {
        builder.AddGet("unit.transform.position", UnitValueCategory.Float3,
            (in LocalTransform component) => UnitValue.FromFloat3(component.Position));
        builder.AddGet("unit.transform.forward", UnitValueCategory.Float3,
            (in LocalTransform component) => UnitValue.FromFloat3(math.mul(component.Rotation, new float3(0f, 0f, 1f))));
        builder.AddGet("unit.transform.scale", UnitValueCategory.Number,
            (in LocalTransform component) => UnitValue.FromFloat(component.Scale));
        builder.AddContextGet("unit.transform.positionOf", UnitValueCategory.Float3, s_targetEntityParameter,
            static (in UnitSourceBindingContext context, in LocalTransform _, UnitValue[] input) =>
            {
                if (!TryGetTargetTransform(context.EntityManager, input, out LocalTransform target))
                    return UnitValue.None;

                return UnitValue.FromFloat3(target.Position);
            });
        builder.AddContextGet("unit.transform.directionTo", UnitValueCategory.Float2, s_targetEntityParameter,
            static (in UnitSourceBindingContext context, in LocalTransform component, UnitValue[] input) =>
            {
                if (!TryGetTargetTransform(context.EntityManager, input, out LocalTransform target))
                    return UnitValue.None;

                return UnitValue.FromFloat2(math.normalizesafe(target.Position.xy - component.Position.xy, float2.zero));
            });
    }

    private static bool TryGetTargetTransform(EntityManager entityManager, UnitValue[] input, out LocalTransform target)
    {
        target = default;
        if (input == null || input.Length != 1 || input[0].Category != UnitValueCategory.Entity)
            return false;

        Entity targetEntity = input[0].Entity;
        if (targetEntity == Entity.Null ||
            !entityManager.Exists(targetEntity) ||
            !entityManager.HasComponent<LocalTransform>(targetEntity))
        {
            return false;
        }

        target = entityManager.GetComponentData<LocalTransform>(targetEntity);
        return true;
    }
}
