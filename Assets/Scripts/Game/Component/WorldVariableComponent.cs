using System;
using System.Collections.Generic;
using Unity.Entities;

public sealed class WorldVariableComponent : IComponentData
{
    public Dictionary<string, UnitValue> Values = new(StringComparer.Ordinal);
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
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!WorldStateUtility.TryGetEntity(entityManager, out Entity worldEntity))
            return false;

        WorldVariableComponent component = GetComponent(entityManager, worldEntity);
        if (component == null)
            return false;

        if (operation == 0)
        {
            result = UnitSourceValue.FromInt(component.Values?.Count ?? 0);
            return true;
        }

        if (!arguments.TryGet(0, out UnitSourceValue keySource))
            return false;

        UnitValue key = keySource.ToUnitValue();
        UnitValue value = operation switch
        {
            1 => UnitValue.FromBool(Contains(component, key)),
            2 => Get(component, key),
            3 => GetCategory(component, key, UnitValueCategory.Number),
            4 => GetCategory(component, key, UnitValueCategory.Bool),
            5 => GetCategory(component, key, UnitValueCategory.Float2),
            6 => GetCategory(component, key, UnitValueCategory.Float3),
            7 => GetCategory(component, key, UnitValueCategory.Entity),
            8 => GetCategory(component, key, UnitValueCategory.String),
            _ => UnitValue.None,
        };
        return UnitSourceValue.TryFromUnitValue(value, out result);
    }

    [UnitSourceSet(0, "world.variables.set", UnitValueCategory.Any,
        ParameterNames = new[] { "Value" }, RequiresKey = true)]
    [UnitSourceSet(1, "world.variables.remove", UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    public static bool TrySet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments)
    {
        if (!WorldStateUtility.TryGetEntity(entityManager, out Entity worldEntity))
            return false;

        WorldVariableComponent component = GetComponent(entityManager, worldEntity);
        if (component == null)
            return false;

        if (operation == 0)
        {
            return arguments.HasKey != 0 && arguments.TryGet(0, out UnitSourceValue value) &&
                   Set(component, arguments.Key.ToString(), value.ToUnitValue());
        }

        return operation == 1 && arguments.TryGet(0, out UnitSourceValue key) &&
               Remove(component, key.ToUnitValue());
    }

    private static WorldVariableComponent GetComponent(EntityManager entityManager, Entity entity)
    {
        return entityManager.Exists(entity) && entityManager.HasComponent<WorldVariableComponent>(entity)
            ? entityManager.GetComponentObject<WorldVariableComponent>(entity)
            : null;
    }

    private static bool Contains(WorldVariableComponent component, UnitValue keyValue)
    {
        return TryGetKey(keyValue, out string key) &&
               component?.Values != null &&
               component.Values.ContainsKey(key);
    }

    private static UnitValue Get(WorldVariableComponent component, UnitValue keyValue)
    {
        if (!TryGetKey(keyValue, out string key) ||
            component?.Values == null ||
            !component.Values.TryGetValue(key, out UnitValue value))
        {
            return UnitValue.None;
        }

        return value;
    }

    private static UnitValue GetCategory(WorldVariableComponent component, UnitValue keyValue, UnitValueCategory category)
    {
        UnitValue value = Get(component, keyValue);
        return value.Category == category ? value : UnitValue.None;
    }

    private static bool Remove(WorldVariableComponent component, UnitValue keyValue)
    {
        return TryGetKey(keyValue, out string key) &&
               component?.Values != null &&
               component.Values.Remove(key);
    }

    private static bool Set(WorldVariableComponent component, string key, UnitValue value)
    {
        if (string.IsNullOrWhiteSpace(key) || value.Category == UnitValueCategory.None)
            return false;

        component.Values ??= new Dictionary<string, UnitValue>(StringComparer.Ordinal);
        component.Values[key] = value;
        return true;
    }

    private static bool TryGetKey(UnitValue value, out string key)
    {
        key = string.Empty;
        return value.TryGetString(out key) && !string.IsNullOrWhiteSpace(key);
    }
}
