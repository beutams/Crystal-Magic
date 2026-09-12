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
    // Entity.Null means this entity owns Values. A non-null owner makes this entity
    // a variable consumer; all variable source access is redirected to that owner.
    public Entity Owner = Entity.Null;
    public Dictionary<string, UnitValue> Values = new(StringComparer.Ordinal);
}

[UnitSourceAuthoring(typeof(UnitVariableAuthoring))]
public sealed class UnitVariableSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_keyParameter =
    {
        new ComparatorParameterDefinition("Key", UnitValueCategory.String),
    };

    private static readonly ComparatorParameterDefinition[] s_ownerParameter =
    {
        new ComparatorParameterDefinition("Owner", UnitValueCategory.Entity),
    };

    public override Type ComponentType => typeof(UnitVariableComponent);

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("unit.variables.count", ComponentType, UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.variables.owner", ComponentType, UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.variables.has", ComponentType, UnitValueCategory.Bool, s_keyParameter);
        schema.AddGet("unit.variables.get", ComponentType, UnitValueCategory.Any, s_keyParameter);
        schema.AddGet("unit.variables.getNumber", ComponentType, UnitValueCategory.Number, s_keyParameter);
        schema.AddGet("unit.variables.getBool", ComponentType, UnitValueCategory.Bool, s_keyParameter);
        schema.AddGet("unit.variables.getFloat2", ComponentType, UnitValueCategory.Float2, s_keyParameter);
        schema.AddGet("unit.variables.getFloat3", ComponentType, UnitValueCategory.Float3, s_keyParameter);
        schema.AddGet("unit.variables.getEntity", ComponentType, UnitValueCategory.Entity, s_keyParameter);
        schema.AddGet("unit.variables.getString", ComponentType, UnitValueCategory.String, s_keyParameter);
        schema.AddSet("unit.variables.set", ComponentType, new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Any) }, requiresKey: true);
        schema.AddSet("unit.variables.remove", ComponentType, s_keyParameter);
        schema.AddSet("unit.variables.setOwner", ComponentType, s_ownerParameter);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitVariableComponent>(entity))
            return;

        table.AddGet(new UnitSourceGet(
            "unit.variables.count",
            UnitValueCategory.Number,
            Array.Empty<ComparatorParameterDefinition>(),
            parameters => UnitValue.FromInt(TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? component.Values?.Count ?? 0
                : 0)));
        table.AddGet(new UnitSourceGet(
            "unit.variables.owner",
            UnitValueCategory.Entity,
            Array.Empty<ComparatorParameterDefinition>(),
            _ => UnitValue.FromEntity(GetOwner(entityManager, entity))));
        table.AddGet(new UnitSourceGet(
            "unit.variables.has",
            UnitValueCategory.Bool,
            s_keyParameter,
            input => UnitValue.FromBool(TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component) && Contains(component, input[0]))));
        table.AddGet(new UnitSourceGet(
            "unit.variables.get",
            UnitValueCategory.Any,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component) ? Get(component, input[0]) : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.variables.getNumber",
            UnitValueCategory.Number,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? GetCategory(component, input[0], UnitValueCategory.Number)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.variables.getBool",
            UnitValueCategory.Bool,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? GetCategory(component, input[0], UnitValueCategory.Bool)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.variables.getFloat2",
            UnitValueCategory.Float2,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? GetCategory(component, input[0], UnitValueCategory.Float2)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.variables.getFloat3",
            UnitValueCategory.Float3,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? GetCategory(component, input[0], UnitValueCategory.Float3)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.variables.getEntity",
            UnitValueCategory.Entity,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? GetCategory(component, input[0], UnitValueCategory.Entity)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.variables.getString",
            UnitValueCategory.String,
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component)
                ? GetCategory(component, input[0], UnitValueCategory.String)
                : UnitValue.None));
        table.AddSet(new UnitSourceSet(
            "unit.variables.set",
            new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Any) },
            (string key, UnitValue value) => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component) && Set(component, key, value)));
        table.AddSet(new UnitSourceSet(
            "unit.variables.remove",
            s_keyParameter,
            input => TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component) && Remove(component, input[0])));
        table.AddSet(new UnitSourceSet(
            "unit.variables.setOwner",
            s_ownerParameter,
            input => SetOwner(entityManager, entity, input[0])));
    }

    public static bool TryResolveOwner(
        EntityManager entityManager,
        Entity entity,
        out Entity ownerEntity,
        out UnitVariableComponent component)
    {
        ownerEntity = Entity.Null;
        component = null;
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(entity))
        {
            return false;
        }

        UnitVariableComponent localComponent = entityManager.GetComponentObject<UnitVariableComponent>(entity);
        Entity owner = localComponent?.Owner ?? Entity.Null;
        if (owner == Entity.Null)
        {
            ownerEntity = entity;
            component = localComponent;
            return component != null;
        }

        if (owner == entity ||
            !entityManager.Exists(owner) ||
            !entityManager.HasComponent<UnitVariableComponent>(owner))
        {
            return false;
        }

        UnitVariableComponent ownerComponent = entityManager.GetComponentObject<UnitVariableComponent>(owner);
        if (ownerComponent == null || ownerComponent.Owner != Entity.Null)
            return false;

        ownerEntity = owner;
        component = ownerComponent;
        return true;
    }

    private static Entity GetOwner(EntityManager entityManager, Entity entity)
    {
        return entityManager.Exists(entity) && entityManager.HasComponent<UnitVariableComponent>(entity)
            ? entityManager.GetComponentObject<UnitVariableComponent>(entity)?.Owner ?? Entity.Null
            : Entity.Null;
    }

    private static bool SetOwner(EntityManager entityManager, Entity entity, UnitValue ownerValue)
    {
        if (ownerValue.Type != UnitValueType.Entity ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(entity))
        {
            return false;
        }

        Entity owner = ownerValue.Entity;

        if (owner != Entity.Null &&
            (owner == entity ||
             !entityManager.Exists(owner) ||
             !entityManager.HasComponent<UnitVariableComponent>(owner) ||
             entityManager.GetComponentObject<UnitVariableComponent>(owner)?.Owner != Entity.Null))
        {
            return false;
        }

        entityManager.GetComponentObject<UnitVariableComponent>(entity).Owner = owner;
        return true;
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
