using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UnitSourceProvider(typeof(LocalTransform), typeof(UnityEngine.Transform))]
public static class UnitTransformSource
{
    [UnitSourceGet(0, "unit.transform.position", UnitValueCategory.Float3)]
    [UnitSourceGet(1, "unit.transform.forward", UnitValueCategory.Float3)]
    [UnitSourceGet(2, "unit.transform.scale", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in LocalTransform component,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromFloat3(component.Position),
            1 => UnitSourceValue.FromFloat3(math.mul(component.Rotation, new float3(0f, 0f, 1f))),
            2 => UnitSourceValue.FromFloat(component.Scale),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(3, "unit.transform.positionOf", UnitValueCategory.Float3, UnitValueCategory.Entity,
        ParameterNames = new[] { "Target Unit" })]
    [UnitSourceGet(4, "unit.transform.directionTo", UnitValueCategory.Float2, UnitValueCategory.Entity,
        ParameterNames = new[] { "Target Unit" })]
    public static bool TryGetRelation(
        int operation,
        Entity entity,
        in ComponentLookup<LocalTransform> transforms,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!transforms.TryGetComponent(entity, out LocalTransform component) ||
            !arguments.TryGetEntity(0, out Entity targetEntity) || targetEntity == Entity.Null ||
            !transforms.TryGetComponent(targetEntity, out LocalTransform target))
        {
            return false;
        }

        switch (operation)
        {
            case 3:
                result = UnitSourceValue.FromFloat3(target.Position);
                return true;
            case 4:
                result = UnitSourceValue.FromFloat2(
                    math.normalizesafe(target.Position.xy - component.Position.xy, float2.zero));
                return true;
            default:
                return false;
        }
    }
}
