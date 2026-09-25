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
            AddComponent(entity, new UnitVariableComponent
            {
                Other = Entity.Null,
            });
            AddBuffer<UnitVariableElement>(entity);
            AddBuffer<UnitVariableConsumerElement>(entity);
        }
    }
}

public struct UnitVariableComponent : IComponentData
{
    // Variables always remain local. Other only supplies the second entity available to source expressions.
    public Entity Other;
}

[UnitSourceProvider(typeof(UnitVariableComponent), typeof(UnitVariableAuthoring))]
public static class UnitVariableSource
{
    [UnitSourceGet(0, "unit.variables.count", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.variables.consumerCount", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.variables.other", UnitValueCategory.Entity)]
    [UnitSourceGet(3, "unit.variables.has", UnitValueCategory.Bool, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(4, "unit.variables.get", UnitValueCategory.Any, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(5, "unit.variables.getNumber", UnitValueCategory.Number, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(6, "unit.variables.getBool", UnitValueCategory.Bool, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(7, "unit.variables.getFloat2", UnitValueCategory.Float2, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(8, "unit.variables.getFloat3", UnitValueCategory.Float3, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(9, "unit.variables.getEntity", UnitValueCategory.Entity, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(10, "unit.variables.getString", UnitValueCategory.String, UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceGet(11, "unit.variables.listEntity", UnitValueCategory.Entity,
        UnitValueCategory.String, UnitValueCategory.Number,
        ParameterNames = new[] { "Key", "Index" })]
    [UnitSourceGet(12, "unit.variables.getNumberOrDefault", UnitValueCategory.Number,
        UnitValueCategory.String, UnitValueCategory.Number,
        ParameterNames = new[] { "Key", "Default" })]
    public static bool TryGet(
        int operation,
        Entity entity,
        in ComponentLookup<UnitVariableComponent> componentLookup,
        in BufferLookup<UnitVariableElement> variableLookup,
        in BufferLookup<UnitVariableConsumerElement> consumerLookup,
        in ComponentLookup<DestroyEntityFlag> destroyLookup,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!componentLookup.HasComponent(entity))
            return false;

        if (operation == 1)
        {
            result = UnitSourceValue.FromInt(CountConsumers(
                entity,
                in componentLookup,
                in consumerLookup,
                in destroyLookup));
            return true;
        }

        if (operation == 2)
        {
            result = UnitSourceValue.FromEntity(componentLookup[entity].Other);
            return true;
        }

        if (!TryGetBuffer(
                entity,
                in componentLookup,
                in variableLookup,
                out DynamicBuffer<UnitVariableElement> variables))
            return false;

        if (operation == 0)
        {
            result = UnitSourceValue.FromInt(variables.Length);
            return true;
        }

        if (!arguments.TryGetString(0, out FixedString128Bytes key))
            return false;

        if (operation == 11)
        {
            if (!arguments.TryGetInt(1, out int index) || index < 0 ||
                !TryBuildListKey(key, "count", out FixedString128Bytes countKey) ||
                !TryGetValue(variables, countKey, out UnitSourceValue countValue) ||
                !countValue.TryGetInt(out int count) || index >= count ||
                !TryBuildListKey(key, index, out FixedString128Bytes entryKey))
            {
                return false;
            }

            result = GetCategory(variables, entryKey, UnitValueCategory.Entity);
            return result.Type != UnitValueType.None;
        }

        if (operation == 12)
        {
            if (!arguments.TryGetNumber(1, out float defaultValue))
                return false;
            result = GetCategory(variables, key, UnitValueCategory.Number);
            if (result.Type == UnitValueType.None)
                result = UnitSourceValue.FromFloat(defaultValue);
            return true;
        }

        UnitSourceValue value = operation switch
        {
            3 => UnitSourceValue.FromBool(Contains(variables, key)),
            4 => Get(variables, key),
            5 => GetCategory(variables, key, UnitValueCategory.Number),
            6 => GetCategory(variables, key, UnitValueCategory.Bool),
            7 => GetCategory(variables, key, UnitValueCategory.Float2),
            8 => GetCategory(variables, key, UnitValueCategory.Float3),
            9 => GetCategory(variables, key, UnitValueCategory.Entity),
            10 => GetCategory(variables, key, UnitValueCategory.String),
            _ => UnitSourceValue.None,
        };
        result = value;
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.variables.set", UnitValueCategory.Any,
        ParameterNames = new[] { "Value" }, RequiresKey = true)]
    [UnitSourceSet(1, "unit.variables.remove", UnitValueCategory.String, ParameterNames = new[] { "Key" })]
    [UnitSourceSet(2, "unit.variables.setOther", UnitValueCategory.Entity, ParameterNames = new[] { "Other" })]
    [UnitSourceSet(3, "unit.variables.addNumber", UnitValueCategory.Number,
        ParameterNames = new[] { "Delta" }, RequiresKey = true)]
    public static bool TrySet(
        int operation,
        Entity entity,
        ref ComponentLookup<UnitVariableComponent> componentLookup,
        ref BufferLookup<UnitVariableElement> variableLookup,
        ref BufferLookup<UnitVariableConsumerElement> consumerLookup,
        in UnitSourceArguments arguments)
    {
        if (operation == 2)
        {
            return arguments.TryGetEntity(0, out Entity other) &&
                   SetOther(entity, other, ref componentLookup, ref consumerLookup);
        }

        if (!TryGetBuffer(
                entity,
                in componentLookup,
                in variableLookup,
                out DynamicBuffer<UnitVariableElement> variables))
            return false;

        if (operation == 0)
        {
            return arguments.HasKey != 0 &&
                   arguments.TryGet(0, out UnitSourceValue sourceValue) &&
                   SetValue(variables, arguments.Key, sourceValue);
        }

        if (operation == 3)
        {
            if (arguments.HasKey == 0 || !arguments.TryGetNumber(0, out float delta))
                return false;

            float current = 0f;
            if (TryGetValue(variables, arguments.Key, out UnitSourceValue currentValue) &&
                !currentValue.TryGetNumber(out current))
            {
                return false;
            }
            return SetValue(variables, arguments.Key, UnitSourceValue.FromFloat(current + delta));
        }

        return operation == 1 && arguments.TryGetString(0, out FixedString128Bytes key) &&
               Remove(variables, key);
    }

    private static bool TryGetBuffer(
        Entity entity,
        in ComponentLookup<UnitVariableComponent> componentLookup,
        in BufferLookup<UnitVariableElement> variableLookup,
        out DynamicBuffer<UnitVariableElement> variables)
    {
        variables = default;
        if (!componentLookup.HasComponent(entity) || !variableLookup.HasBuffer(entity))
            return false;

        variables = variableLookup[entity];
        return true;
    }

    private static bool TryBuildListKey(
        in FixedString128Bytes prefix,
        int index,
        out FixedString128Bytes key)
    {
        key = prefix;
        return key.Append('.') == FormatError.None &&
               key.Append(index) == FormatError.None;
    }

    private static bool TryBuildListKey(
        in FixedString128Bytes prefix,
        in FixedString32Bytes suffix,
        out FixedString128Bytes key)
    {
        key = prefix;
        return key.Append('.') == FormatError.None &&
               key.Append(suffix) == FormatError.None;
    }

    private static bool SetOther(
        Entity entity,
        Entity other,
        ref ComponentLookup<UnitVariableComponent> componentLookup,
        ref BufferLookup<UnitVariableConsumerElement> consumerLookup)
    {
        if (!componentLookup.HasComponent(entity) || !consumerLookup.HasBuffer(entity))
            return false;

        if (other != Entity.Null &&
            (other == entity ||
             !componentLookup.HasComponent(other) ||
             !consumerLookup.HasBuffer(other)))
        {
            return false;
        }

        UnitVariableComponent component = componentLookup[entity];
        if (component.Other == other)
            return true;

        RemoveConsumer(component.Other, entity, ref consumerLookup);
        AddConsumer(other, entity, ref consumerLookup);
        component.Other = other;
        componentLookup[entity] = component;
        return true;
    }

    private static int CountConsumers(
        Entity source,
        in ComponentLookup<UnitVariableComponent> componentLookup,
        in BufferLookup<UnitVariableConsumerElement> consumerLookup,
        in ComponentLookup<DestroyEntityFlag> destroyLookup)
    {
        if (source == Entity.Null || !consumerLookup.HasBuffer(source))
            return 0;

        DynamicBuffer<UnitVariableConsumerElement> consumers = consumerLookup[source];
        int count = 0;
        for (int index = 0; index < consumers.Length; index++)
        {
            Entity consumer = consumers[index].Value;
            if (!componentLookup.HasComponent(consumer) || componentLookup[consumer].Other != source)
                continue;
            if (destroyLookup.HasComponent(consumer) && destroyLookup.IsComponentEnabled(consumer))
                continue;

            count++;
        }

        return count;
    }

    private static void AddConsumer(
        Entity source,
        Entity consumer,
        ref BufferLookup<UnitVariableConsumerElement> consumerLookup)
    {
        if (source == Entity.Null || !consumerLookup.HasBuffer(source))
            return;

        DynamicBuffer<UnitVariableConsumerElement> consumers = consumerLookup[source];
        for (int index = 0; index < consumers.Length; index++)
        {
            if (consumers[index].Value == consumer)
                return;
        }

        consumers.Add(new UnitVariableConsumerElement { Value = consumer });
    }

    private static void RemoveConsumer(
        Entity source,
        Entity consumer,
        ref BufferLookup<UnitVariableConsumerElement> consumerLookup)
    {
        if (source == Entity.Null || !consumerLookup.HasBuffer(source))
            return;

        DynamicBuffer<UnitVariableConsumerElement> consumers = consumerLookup[source];
        for (int index = consumers.Length - 1; index >= 0; index--)
        {
            if (consumers[index].Value == consumer)
                consumers.RemoveAtSwapBack(index);
        }
    }

    public static Entity GetOther(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(entity))
        {
            return Entity.Null;
        }

        Entity other = entityManager.GetComponentData<UnitVariableComponent>(entity).Other;
        return other != Entity.Null && entityManager.Exists(other) ? other : Entity.Null;
    }

    public static bool TryGetBuffer(
        EntityManager entityManager,
        Entity entity,
        out DynamicBuffer<UnitVariableElement> variables)
    {
        variables = default;
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(entity) ||
            !entityManager.HasBuffer<UnitVariableElement>(entity))
        {
            return false;
        }

        variables = entityManager.GetBuffer<UnitVariableElement>(entity);
        return true;
    }

    public static bool TryGetValue(
        EntityManager entityManager,
        Entity entity,
        string key,
        out UnitSourceValue value)
    {
        value = default;
        return TryCreateKey(key, out FixedString128Bytes fixedKey) &&
               TryGetBuffer(entityManager, entity, out DynamicBuffer<UnitVariableElement> variables) &&
               TryGetValue(variables, fixedKey, out value);
    }

    public static bool TrySetValue(
        EntityManager entityManager,
        Entity entity,
        string key,
        in UnitValue value)
    {
        return UnitSourceValue.TryFromUnitValue(value, out UnitSourceValue sourceValue) &&
               TrySetValue(entityManager, entity, key, sourceValue);
    }

    public static bool TrySetValue(
        EntityManager entityManager,
        Entity entity,
        string key,
        in UnitSourceValue value)
    {
        return TryCreateKey(key, out FixedString128Bytes fixedKey) &&
               TryGetBuffer(entityManager, entity, out DynamicBuffer<UnitVariableElement> variables) &&
               SetValue(variables, fixedKey, value);
    }

    public static int CountConsumers(EntityManager entityManager, Entity source)
    {
        if (source == Entity.Null ||
            !entityManager.Exists(source) ||
            !entityManager.HasBuffer<UnitVariableConsumerElement>(source))
            return 0;

        DynamicBuffer<UnitVariableConsumerElement> consumers = entityManager.GetBuffer<UnitVariableConsumerElement>(source);
        int count = 0;
        for (int index = 0; index < consumers.Length; index++)
        {
            Entity candidate = consumers[index].Value;
            if (!entityManager.Exists(candidate) ||
                !entityManager.HasComponent<UnitVariableComponent>(candidate) ||
                entityManager.GetComponentData<UnitVariableComponent>(candidate).Other != source ||
                entityManager.HasComponent<DestroyEntityFlag>(candidate) &&
                entityManager.IsComponentEnabled<DestroyEntityFlag>(candidate))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public static bool SetOther(EntityManager entityManager, Entity entity, Entity other)
    {
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(entity) ||
            !entityManager.HasBuffer<UnitVariableElement>(entity) ||
            !entityManager.HasBuffer<UnitVariableConsumerElement>(entity))
        {
            return false;
        }

        if (other != Entity.Null &&
            (other == entity ||
             !entityManager.Exists(other) ||
             !entityManager.HasComponent<UnitVariableComponent>(other) ||
             !entityManager.HasBuffer<UnitVariableElement>(other) ||
             !entityManager.HasBuffer<UnitVariableConsumerElement>(other)))
        {
            return false;
        }

        UnitVariableComponent component = entityManager.GetComponentData<UnitVariableComponent>(entity);
        if (component.Other == other)
            return true;

        RemoveConsumer(entityManager, component.Other, entity);
        AddConsumer(entityManager, other, entity);
        component.Other = other;
        entityManager.SetComponentData(entity, component);
        return true;
    }

    private static void AddConsumer(EntityManager entityManager, Entity source, Entity consumer)
    {
        if (source == Entity.Null ||
            !entityManager.Exists(source) ||
            !entityManager.HasBuffer<UnitVariableConsumerElement>(source))
        {
            return;
        }

        DynamicBuffer<UnitVariableConsumerElement> consumers = entityManager.GetBuffer<UnitVariableConsumerElement>(source);
        for (int index = 0; index < consumers.Length; index++)
        {
            if (consumers[index].Value == consumer)
                return;
        }

        consumers.Add(new UnitVariableConsumerElement { Value = consumer });
    }

    private static void RemoveConsumer(EntityManager entityManager, Entity source, Entity consumer)
    {
        if (source == Entity.Null ||
            !entityManager.Exists(source) ||
            !entityManager.HasBuffer<UnitVariableConsumerElement>(source))
        {
            return;
        }

        DynamicBuffer<UnitVariableConsumerElement> consumers = entityManager.GetBuffer<UnitVariableConsumerElement>(source);
        for (int index = consumers.Length - 1; index >= 0; index--)
        {
            if (consumers[index].Value == consumer)
                consumers.RemoveAtSwapBack(index);
        }
    }

    public static bool TryGetValue(
        in DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key,
        out UnitSourceValue value)
    {
        int index = FindIndex(variables, key);
        if (index >= 0)
        {
            value = variables[index].Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool Contains(
        in DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key)
    {
        return FindIndex(variables, key) >= 0;
    }

    private static UnitSourceValue Get(
        in DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key)
    {
        return TryGetValue(variables, key, out UnitSourceValue value)
            ? value
            : UnitSourceValue.None;
    }

    private static UnitSourceValue GetCategory(
        in DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key,
        UnitValueCategory category)
    {
        UnitSourceValue value = Get(variables, key);
        return value.Category == category ? value : UnitSourceValue.None;
    }

    private static bool Remove(
        DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key)
    {
        int index = FindIndex(variables, key);
        if (index < 0)
            return false;

        variables.RemoveAtSwapBack(index);
        return true;
    }

    public static bool SetValue(
        DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key,
        in UnitSourceValue value)
    {
        if (key.Length == 0 || value.Category == UnitValueCategory.None)
            return false;

        UnitVariableElement element = new()
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

    private static int FindIndex(
        in DynamicBuffer<UnitVariableElement> variables,
        in FixedString128Bytes key)
    {
        for (int index = 0; index < variables.Length; index++)
        {
            if (variables[index].Key.Equals(key))
                return index;
        }

        return -1;
    }

    private static bool TryCreateKey(string source, out FixedString128Bytes key)
    {
        key = default;
        return !string.IsNullOrWhiteSpace(source) && key.CopyFrom(source) == CopyError.None;
    }
}
