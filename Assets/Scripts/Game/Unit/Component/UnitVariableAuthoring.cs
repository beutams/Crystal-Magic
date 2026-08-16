using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public sealed class UnitVariableAuthoring : MonoBehaviour
{
    private sealed class UnitVariableBaker : Baker<UnitVariableAuthoring>
    {
        public override void Bake(UnitVariableAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitVariableComponent());
        }
    }
}

public sealed class UnitVariableComponent : IComponentData
{
    public Dictionary<string, UnitValue> Values = new(StringComparer.Ordinal);
}

[UnitSourceAuthoring(typeof(UnitVariableAuthoring))]
public sealed class UnitVariableSource : UnitManagedComponentSource<UnitVariableComponent>
{
    private static readonly ComparatorParameterDefinition[] s_keyParameter =
    {
        new ComparatorParameterDefinition("Key", UnitValueCategory.String),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitVariableComponent> builder)
    {
        builder.AddGet("unit.variables.count", UnitValueCategory.Number,
            (in UnitVariableComponent component) => UnitValue.FromInt(component.Values?.Count ?? 0));
        builder.AddGet("unit.variables.has", UnitValueCategory.Bool, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => UnitValue.FromBool(Contains(component, input[0])));
        builder.AddGet("unit.variables.get", UnitValueCategory.Any, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => Get(component, input[0]));
        builder.AddGet("unit.variables.getNumber", UnitValueCategory.Number, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => GetCategory(component, input[0], UnitValueCategory.Number));
        builder.AddGet("unit.variables.getBool", UnitValueCategory.Bool, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => GetCategory(component, input[0], UnitValueCategory.Bool));
        builder.AddGet("unit.variables.getFloat2", UnitValueCategory.Float2, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => GetCategory(component, input[0], UnitValueCategory.Float2));
        builder.AddGet("unit.variables.getFloat3", UnitValueCategory.Float3, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => GetCategory(component, input[0], UnitValueCategory.Float3));
        builder.AddGet("unit.variables.getEntity", UnitValueCategory.Entity, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => GetCategory(component, input[0], UnitValueCategory.Entity));
        builder.AddGet("unit.variables.getString", UnitValueCategory.String, s_keyParameter,
            (in UnitVariableComponent component, UnitValue[] input) => GetCategory(component, input[0], UnitValueCategory.String));

        builder.AddKeyedSet("unit.variables.set", UnitValueCategory.Any,
            (ref UnitVariableComponent component, string key, UnitValue value) => Set(component, key, value));
        builder.AddSet("unit.variables.remove", UnitValueCategory.String,
            (ref UnitVariableComponent component, UnitValue key) => Remove(component, key));
    }

    private static bool Contains(UnitVariableComponent component, UnitValue keyValue)
    {
        return TryGetKey(keyValue, out string key) &&
               component?.Values != null &&
               component.Values.ContainsKey(key);
    }

    private static UnitValue Get(UnitVariableComponent component, UnitValue keyValue)
    {
        if (!TryGetKey(keyValue, out string key) ||
            component?.Values == null ||
            !component.Values.TryGetValue(key, out UnitValue value))
        {
            return UnitValue.None;
        }

        return value;
    }

    private static UnitValue GetCategory(UnitVariableComponent component, UnitValue keyValue, UnitValueCategory category)
    {
        UnitValue value = Get(component, keyValue);
        return value.Category == category ? value : UnitValue.None;
    }

    private static bool Remove(UnitVariableComponent component, UnitValue keyValue)
    {
        return TryGetKey(keyValue, out string key) &&
               component?.Values != null &&
               component.Values.Remove(key);
    }

    private static bool Set(UnitVariableComponent component, string key, UnitValue value)
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
