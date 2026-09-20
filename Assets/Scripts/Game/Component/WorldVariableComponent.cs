using Unity.Collections;
using Unity.Entities;

public struct WorldVariableComponent : IComponentData
{
}

[UnitSourceProvider(typeof(WorldVariableComponent), isGlobal: true)]
public static class WorldVariableSource
{
    [UnitSourceGet(0, "world.variables.count", UnitValueCategory.Number)]
    [UnitSourceGet(1, "world.variables.has", UnitValueCategory.Bool, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(2, "world.variables.get", UnitValueCategory.Any, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(3, "world.variables.getNumber", UnitValueCategory.Number, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(4, "world.variables.getBool", UnitValueCategory.Bool, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(5, "world.variables.getFloat2", UnitValueCategory.Float2, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(6, "world.variables.getFloat3", UnitValueCategory.Float3, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(7, "world.variables.getEntity", UnitValueCategory.Entity, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(8, "world.variables.getString", UnitValueCategory.String, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    public static bool TryGet(
        int operation,
        Entity entity,
        in ComponentLookup<WorldVariableComponent> componentLookup,
        in BufferLookup<WorldVariableElement> variableLookup,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!componentLookup.HasComponent(entity) || !variableLookup.HasBuffer(entity))
            return false;

        DynamicBuffer<WorldVariableElement> variables = variableLookup[entity];
        if (operation == 0)
        {
            result = UnitSourceValue.FromInt(variables.Length);
            return true;
        }

        if (!arguments.TryGetString(0, out FixedString128Bytes key))
            return false;

        UnitSourceValue value = operation switch
        {
            1 => UnitSourceValue.FromBool(FindIndex(variables, key) >= 0),
            2 => Get(variables, key),
            3 => GetCategory(variables, key, UnitValueCategory.Number),
            4 => GetCategory(variables, key, UnitValueCategory.Bool),
            5 => GetCategory(variables, key, UnitValueCategory.Float2),
            6 => GetCategory(variables, key, UnitValueCategory.Float3),
            7 => GetCategory(variables, key, UnitValueCategory.Entity),
            8 => GetCategory(variables, key, UnitValueCategory.String),
            _ => UnitSourceValue.None,
        };
        result = value;
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "world.variables.set", UnitValueCategory.Any,
        ParameterNames = new[] { "Value" }, RequiresKey = true)]
    [UnitSourceSet(1, "world.variables.remove", UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    public static bool TrySet(
        int operation,
        Entity entity,
        in ComponentLookup<WorldVariableComponent> componentLookup,
        ref BufferLookup<WorldVariableElement> variableLookup,
        in UnitSourceArguments arguments)
    {
        if (!componentLookup.HasComponent(entity) || !variableLookup.HasBuffer(entity))
            return false;

        DynamicBuffer<WorldVariableElement> variables = variableLookup[entity];
        if (operation == 0)
        {
            return arguments.HasKey != 0 &&
                   arguments.TryGet(0, out UnitSourceValue value) &&
                   Set(variables, arguments.Key, value);
        }

        return operation == 1 &&
               arguments.TryGetString(0, out FixedString128Bytes key) &&
               Remove(variables, key);
    }

    private static UnitSourceValue Get(
        in DynamicBuffer<WorldVariableElement> variables,
        in FixedString128Bytes key)
    {
        int index = FindIndex(variables, key);
        return index >= 0 ? variables[index].Value : UnitSourceValue.None;
    }

    private static UnitSourceValue GetCategory(
        in DynamicBuffer<WorldVariableElement> variables,
        in FixedString128Bytes key,
        UnitValueCategory category)
    {
        UnitSourceValue value = Get(variables, key);
        return value.Category == category ? value : UnitSourceValue.None;
    }

    private static bool Set(
        DynamicBuffer<WorldVariableElement> variables,
        in FixedString128Bytes key,
        in UnitSourceValue value)
    {
        if (key.Length == 0 || value.Category == UnitValueCategory.None)
            return false;

        WorldVariableElement element = new()
        {
            Key = key,
            Value = value,
        };
        int index = FindIndex(variables, key);
        if (index >= 0)
            variables[index] = element;
        else
            variables.Add(element);

        return true;
    }

    private static bool Remove(
        DynamicBuffer<WorldVariableElement> variables,
        in FixedString128Bytes key)
    {
        int index = FindIndex(variables, key);
        if (index < 0)
            return false;

        variables.RemoveAtSwapBack(index);
        return true;
    }

    private static int FindIndex(
        in DynamicBuffer<WorldVariableElement> variables,
        in FixedString128Bytes key)
    {
        for (int index = 0; index < variables.Length; index++)
        {
            if (variables[index].Key.Equals(key))
                return index;
        }

        return -1;
    }
}
