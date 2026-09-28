using Unity.Entities;
using Unity.Mathematics;

// Random values are derived from the evaluated entity and an explicit sequence value.
// Callers retain the sequence in a unit variable when they need a new value later,
// which keeps the result deterministic across simulations.
[UnitSourceProvider(typeof(UnitVariableComponent), typeof(UnitVariableAuthoring))]
public static class UnitRandomSource
{
    [UnitSourceGet(0, "unit.random.number", UnitValueCategory.Number,
        UnitValueCategory.Number, UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "Sequence", "Minimum", "Maximum" })]
    [UnitSourceGet(1, "unit.random.integer", UnitValueCategory.Number,
        UnitValueCategory.Number, UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "Sequence", "Minimum", "Maximum" })]
    [UnitSourceGet(2, "unit.random.bool", UnitValueCategory.Bool,
        UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "Sequence", "True Chance" })]
    [UnitSourceGet(3, "unit.random.float2", UnitValueCategory.Float2,
        UnitValueCategory.Number, UnitValueCategory.Float2, UnitValueCategory.Float2,
        ParameterNames = new[] { "Sequence", "Minimum", "Maximum" })]
    [UnitSourceGet(4, "unit.random.float3", UnitValueCategory.Float3,
        UnitValueCategory.Number, UnitValueCategory.Float3, UnitValueCategory.Float3,
        ParameterNames = new[] { "Sequence", "Minimum", "Maximum" })]
    [UnitSourceGet(5, "unit.random.direction2", UnitValueCategory.Float2,
        UnitValueCategory.Number, ParameterNames = new[] { "Sequence" })]
    [UnitSourceGet(6, "unit.random.direction3", UnitValueCategory.Float3,
        UnitValueCategory.Number, ParameterNames = new[] { "Sequence" })]
    public static bool TryGet(
        int operation,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!arguments.TryGetNumber(0, out float sequence))
            return false;

        Random random = CreateRandom(entity, sequence);
        switch (operation)
        {
            case 0 when arguments.TryGetNumber(1, out float minimum) &&
                        arguments.TryGetNumber(2, out float maximum):
                result = UnitSourceValue.FromFloat(random.NextFloat(math.min(minimum, maximum), math.max(minimum, maximum)));
                return true;
            case 1 when arguments.TryGetNumber(1, out float minimum) &&
                        arguments.TryGetNumber(2, out float maximum):
                int minInteger = (int)math.ceil(math.min(minimum, maximum));
                int maxInteger = (int)math.floor(math.max(minimum, maximum));
                result = UnitSourceValue.FromFloat(
                    minInteger >= maxInteger ? minInteger : random.NextInt(minInteger, maxInteger + 1));
                return true;
            case 2 when arguments.TryGetNumber(1, out float trueChance):
                result = UnitSourceValue.FromBool(random.NextFloat() < math.saturate(trueChance));
                return true;
            case 3 when arguments.TryGetFloat2(1, out float2 minFloat2) &&
                        arguments.TryGetFloat2(2, out float2 maxFloat2):
                result = UnitSourceValue.FromFloat2(NextFloat2(ref random, minFloat2, maxFloat2));
                return true;
            case 4 when arguments.TryGetFloat3(1, out float3 minFloat3) &&
                        arguments.TryGetFloat3(2, out float3 maxFloat3):
                result = UnitSourceValue.FromFloat3(NextFloat3(ref random, minFloat3, maxFloat3));
                return true;
            case 5:
                result = UnitSourceValue.FromFloat2(random.NextFloat2Direction());
                return true;
            case 6:
                result = UnitSourceValue.FromFloat3(random.NextFloat3Direction());
                return true;
            default:
                return false;
        }
    }

    private static Random CreateRandom(Entity entity, float sequence)
    {
        uint seed = math.hash(new uint3(
            unchecked((uint)entity.Index),
            unchecked((uint)entity.Version),
            math.asuint(sequence)));
        return Random.CreateFromIndex(math.max(1u, seed));
    }

    private static float2 NextFloat2(ref Random random, float2 first, float2 second)
    {
        float2 minimum = math.min(first, second);
        float2 maximum = math.max(first, second);
        return new float2(
            random.NextFloat(minimum.x, maximum.x),
            random.NextFloat(minimum.y, maximum.y));
    }

    private static float3 NextFloat3(ref Random random, float3 first, float3 second)
    {
        float3 minimum = math.min(first, second);
        float3 maximum = math.max(first, second);
        return new float3(
            random.NextFloat(minimum.x, maximum.x),
            random.NextFloat(minimum.y, maximum.y),
            random.NextFloat(minimum.z, maximum.z));
    }
}
