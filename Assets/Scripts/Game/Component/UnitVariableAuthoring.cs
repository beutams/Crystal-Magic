using System;
using System.Collections.Generic;
using Unity.Collections;
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

[UnitSourceProvider(typeof(UnitVariableComponent), typeof(UnitVariableAuthoring))]
public static class UnitVariableSource
{
    [UnitSourceGet(0, "unit.variables.count", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.variables.consumerCount", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.variables.owner", UnitValueCategory.Entity)]
    [UnitSourceGet(3, "unit.variables.has", UnitValueCategory.Bool, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(4, "unit.variables.get", UnitValueCategory.Any, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(5, "unit.variables.getNumber", UnitValueCategory.Number, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(6, "unit.variables.getBool", UnitValueCategory.Bool, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(7, "unit.variables.getFloat2", UnitValueCategory.Float2, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(8, "unit.variables.getFloat3", UnitValueCategory.Float3, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(9, "unit.variables.getEntity", UnitValueCategory.Entity, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(10, "unit.variables.getString", UnitValueCategory.String, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitVariableComponent>(entity))
            return false;

        if (operation == 1)
        {
            result = UnitSourceValue.FromInt(CountConsumers(entityManager, entity));
            return true;
        }

        if (operation == 2)
        {
            result = UnitSourceValue.FromEntity(GetOwner(entityManager, entity));
            return true;
        }

        if (!TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component))
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
            3 => UnitValue.FromBool(Contains(component, key)),
            4 => Get(component, key),
            5 => GetCategory(component, key, UnitValueCategory.Number),
            6 => GetCategory(component, key, UnitValueCategory.Bool),
            7 => GetCategory(component, key, UnitValueCategory.Float2),
            8 => GetCategory(component, key, UnitValueCategory.Float3),
            9 => GetCategory(component, key, UnitValueCategory.Entity),
            10 => GetCategory(component, key, UnitValueCategory.String),
            _ => UnitValue.None,
        };
        return UnitSourceValue.TryFromUnitValue(value, out result);
    }

    [UnitSourceSet(0, "unit.variables.set", UnitValueCategory.Any,
        ParameterNames = new[] { "Value" }, RequiresKey = true)]
    [UnitSourceSet(1, "unit.variables.remove", UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceSet(2, "unit.variables.setOwner", UnitValueCategory.Entity, ParameterNames = new[] { "Owner" })]
    public static bool TrySet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments)
    {
        if (operation == 2)
        {
            return arguments.TryGet(0, out UnitSourceValue owner) &&
                   SetOwner(entityManager, entity, owner.ToUnitValue());
        }

        if (!TryResolveOwner(entityManager, entity, out _, out UnitVariableComponent component))
            return false;

        if (operation == 0)
        {
            return arguments.HasKey != 0 &&
                   arguments.TryGet(0, out UnitSourceValue sourceValue) &&
                   Set(component, arguments.Key.ToString(), sourceValue.ToUnitValue());
        }

        return operation == 1 && arguments.TryGet(0, out UnitSourceValue key) &&
               Remove(component, key.ToUnitValue());
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

    public static int CountConsumers(EntityManager entityManager, Entity owner)
    {
        if (owner == Entity.Null || !entityManager.Exists(owner))
            return 0;

        EntityQuery query = entityManager.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<UnitVariableComponent>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        int count = 0;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity candidate = entities[index];
            if (candidate == owner ||
                entityManager.HasComponent<DestroyEntityFlag>(candidate) &&
                entityManager.IsComponentEnabled<DestroyEntityFlag>(candidate))
            {
                continue;
            }

            UnitVariableComponent variables = entityManager.GetComponentObject<UnitVariableComponent>(candidate);
            if (variables?.Owner == owner)
                count++;
        }

        return count;
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
