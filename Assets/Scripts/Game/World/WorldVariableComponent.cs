using System;
using System.Collections.Generic;
using Unity.Entities;

public sealed class WorldVariableComponent : IComponentData
{
    public Dictionary<string, UnitValue> Values = new(StringComparer.Ordinal);
}

public sealed class WorldVariableSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_keyParameter =
    {
        new ComparatorParameterDefinition("Key", UnitValueCategory.String),
    };

    private static readonly ComparatorParameterDefinition[] s_valueParameter =
    {
        new ComparatorParameterDefinition("Value", UnitValueCategory.Any),
    };

    public override Type ComponentType => typeof(WorldVariableComponent);
    public override bool IsGlobal => true;

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("world.variables.count", ComponentType, UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("world.variables.has", ComponentType, UnitValueCategory.Bool, s_keyParameter);
        schema.AddGet("world.variables.get", ComponentType, UnitValueCategory.Any, s_keyParameter);
        schema.AddGet("world.variables.getNumber", ComponentType, UnitValueCategory.Number, s_keyParameter);
        schema.AddGet("world.variables.getBool", ComponentType, UnitValueCategory.Bool, s_keyParameter);
        schema.AddGet("world.variables.getFloat2", ComponentType, UnitValueCategory.Float2, s_keyParameter);
        schema.AddGet("world.variables.getFloat3", ComponentType, UnitValueCategory.Float3, s_keyParameter);
        schema.AddGet("world.variables.getEntity", ComponentType, UnitValueCategory.Entity, s_keyParameter);
        schema.AddGet("world.variables.getString", ComponentType, UnitValueCategory.String, s_keyParameter);
        schema.AddSet("world.variables.set", ComponentType, s_valueParameter, requiresKey: true);
        schema.AddSet("world.variables.remove", ComponentType, s_keyParameter);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        if (!WorldStateUtility.TryGetEntity(context.EntityManager, out Entity worldEntity))
            throw new InvalidOperationException("World state entity must exist before unit sources are initialized.");

        EntityManager entityManager = context.EntityManager;
        table.AddGet(new UnitSourceGet(
            "world.variables.count",
            UnitValueCategory.Number,
            Array.Empty<ComparatorParameterDefinition>(),
            _ => UnitValue.FromInt(GetComponent(entityManager, worldEntity)?.Values?.Count ?? 0)));
        table.AddGet(new UnitSourceGet(
            "world.variables.has",
            UnitValueCategory.Bool,
            s_keyParameter,
            input => UnitValue.FromBool(Contains(GetComponent(entityManager, worldEntity), input[0]))));
        table.AddGet(new UnitSourceGet(
            "world.variables.get",
            UnitValueCategory.Any,
            s_keyParameter,
            input => Get(GetComponent(entityManager, worldEntity), input[0])));
        table.AddGet(new UnitSourceGet(
            "world.variables.getNumber",
            UnitValueCategory.Number,
            s_keyParameter,
            input => GetCategory(GetComponent(entityManager, worldEntity), input[0], UnitValueCategory.Number)));
        table.AddGet(new UnitSourceGet(
            "world.variables.getBool",
            UnitValueCategory.Bool,
            s_keyParameter,
            input => GetCategory(GetComponent(entityManager, worldEntity), input[0], UnitValueCategory.Bool)));
        table.AddGet(new UnitSourceGet(
            "world.variables.getFloat2",
            UnitValueCategory.Float2,
            s_keyParameter,
            input => GetCategory(GetComponent(entityManager, worldEntity), input[0], UnitValueCategory.Float2)));
        table.AddGet(new UnitSourceGet(
            "world.variables.getFloat3",
            UnitValueCategory.Float3,
            s_keyParameter,
            input => GetCategory(GetComponent(entityManager, worldEntity), input[0], UnitValueCategory.Float3)));
        table.AddGet(new UnitSourceGet(
            "world.variables.getEntity",
            UnitValueCategory.Entity,
            s_keyParameter,
            input => GetCategory(GetComponent(entityManager, worldEntity), input[0], UnitValueCategory.Entity)));
        table.AddGet(new UnitSourceGet(
            "world.variables.getString",
            UnitValueCategory.String,
            s_keyParameter,
            input => GetCategory(GetComponent(entityManager, worldEntity), input[0], UnitValueCategory.String)));
        table.AddSet(new UnitSourceSet(
            "world.variables.set",
            s_valueParameter,
            (string key, UnitValue value) => Set(GetComponent(entityManager, worldEntity), key, value)));
        table.AddSet(new UnitSourceSet(
            "world.variables.remove",
            s_keyParameter,
            input => Remove(GetComponent(entityManager, worldEntity), input[0])));
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
