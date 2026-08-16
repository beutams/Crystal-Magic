using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public readonly struct UnitSourceBindingContext
{
    public UnitSourceBindingContext(Entity entity, EntityManager entityManager)
    {
        Entity = entity;
        EntityManager = entityManager;
    }

    public Entity Entity { get; }
    public EntityManager EntityManager { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UnitSourceAuthoringAttribute : Attribute
{
    public UnitSourceAuthoringAttribute(Type authoringType)
    {
        AuthoringType = authoringType;
    }

    public Type AuthoringType { get; }
}

public static class UnitSourceSchemaFactory
{
    public static UnitSourceSchema CreateForPrefab(GameObject prefab)
    {
        UnitSourceSchemaBuilder builder = new();
        if (prefab == null)
            return builder.Build();

        IReadOnlyList<UnitComponentSource> sources = UnitComponentSourceRegistry.Sources;
        for (int i = 0; i < sources.Count; i++)
        {
            UnitComponentSource source = sources[i];
            if (source == null)
                continue;

            if (source.IsGlobal)
            {
                source.Describe(builder);
                continue;
            }

            UnitSourceAuthoringAttribute[] bindings = (UnitSourceAuthoringAttribute[])Attribute.GetCustomAttributes(
                source.GetType(), typeof(UnitSourceAuthoringAttribute), inherit: false);
            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                Type authoringType = bindings[bindingIndex].AuthoringType;
                if (authoringType == null || prefab.GetComponentInChildren(authoringType, true) == null)
                    continue;

                source.Describe(builder);
                break;
            }
        }

        return builder.Build();
    }
}

public abstract class UnitComponentSource
{
    public abstract Type ComponentType { get; }
    public virtual bool IsGlobal => false;
    public abstract void Describe(UnitSourceSchemaBuilder schema);
    public abstract void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table);
}

public delegate UnitValue ComponentValueGetter<TComponent>(in TComponent component);
public delegate UnitValue ComponentGetter<TComponent>(in TComponent component, UnitValue[] parameters);
public delegate bool ComponentValueSetter<TComponent>(ref TComponent component, UnitValue value);
public delegate bool ComponentKeyedValueSetter<TComponent>(ref TComponent component, string key, UnitValue value);

public sealed class UnitSourceDefinitionBuilder<TComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();

    private readonly Dictionary<string, UnitSourceGetDefinition<TComponent>> _gets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitSourceSetDefinition<TComponent>> _sets = new(StringComparer.Ordinal);

    public void AddGet(string key, UnitValueCategory returnType, ComponentValueGetter<TComponent> getter)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));

        AddGet(key, returnType, s_noParameters, (in TComponent component, UnitValue[] _) => getter(in component));
    }

    public void AddGet(
        string key,
        UnitValueCategory returnType,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        ComponentGetter<TComponent> getter)
    {
        if (getter == null)
            throw new ArgumentNullException(nameof(getter));

        ValidateKey(key, "get");
        if (returnType == UnitValueCategory.None)
            throw new ArgumentOutOfRangeException(nameof(returnType));

        ValidateParameters(parameters);
        if (!_gets.TryAdd(key, new UnitSourceGetDefinition<TComponent>(key, returnType, parameters, getter)))
            throw new InvalidOperationException($"Unit source get is already defined: {key}");
    }

    public void AddSet(string key, UnitValueCategory valueType, ComponentValueSetter<TComponent> setter)
    {
        if (setter == null)
            throw new ArgumentNullException(nameof(setter));

        ValidateKey(key, "set");
        if (valueType == UnitValueCategory.None)
            throw new ArgumentOutOfRangeException(nameof(valueType));

        if (!_sets.TryAdd(key, new UnitSourceSetDefinition<TComponent>(key, valueType, setter)))
            throw new InvalidOperationException($"Unit source set is already defined: {key}");
    }

    public void AddKeyedSet(string key, UnitValueCategory valueType, ComponentKeyedValueSetter<TComponent> setter)
    {
        if (setter == null)
            throw new ArgumentNullException(nameof(setter));

        ValidateKey(key, "set");
        if (valueType == UnitValueCategory.None)
            throw new ArgumentOutOfRangeException(nameof(valueType));

        if (!_sets.TryAdd(key, new UnitSourceSetDefinition<TComponent>(key, valueType, setter)))
            throw new InvalidOperationException($"Unit source set is already defined: {key}");
    }

    internal UnitSourceGetDefinition<TComponent>[] BuildGets()
    {
        UnitSourceGetDefinition<TComponent>[] result = new UnitSourceGetDefinition<TComponent>[_gets.Count];
        _gets.Values.CopyTo(result, 0);
        return result;
    }

    internal UnitSourceSetDefinition<TComponent>[] BuildSets()
    {
        UnitSourceSetDefinition<TComponent>[] result = new UnitSourceSetDefinition<TComponent>[_sets.Count];
        _sets.Values.CopyTo(result, 0);
        return result;
    }

    private static void ValidateKey(string key, string entryType)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException($"Unit source {entryType} key cannot be empty.", nameof(key));
    }

    private static void ValidateParameters(IReadOnlyList<ComparatorParameterDefinition> parameters)
    {
        if (parameters == null)
            return;

        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Category == UnitValueCategory.None)
                throw new ArgumentOutOfRangeException(nameof(parameters));
        }
    }
}

public abstract class UnitComponentSource<TComponent> : UnitComponentSource
    where TComponent : unmanaged, IComponentData
{
    private UnitSourceGetDefinition<TComponent>[] _gets;
    private UnitSourceSetDefinition<TComponent>[] _sets;

    public sealed override Type ComponentType => typeof(TComponent);

    public sealed override void Describe(UnitSourceSchemaBuilder schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        UnitSourceGetDefinition<TComponent>[] gets = GetGets();
        for (int i = 0; i < gets.Length; i++)
            schema.AddGet(gets[i].Key, ComponentType, gets[i].ReturnType, gets[i].Parameters);

        UnitSourceSetDefinition<TComponent>[] sets = GetSets();
        for (int i = 0; i < sets.Length; i++)
            schema.AddSet(sets[i].Key, ComponentType, sets[i].Parameters, sets[i].RequiresKey);
    }

    public sealed override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));

        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
            return;

        UnitSourceGetDefinition<TComponent>[] gets = GetGets();
        for (int i = 0; i < gets.Length; i++)
            AddGet(table, entityManager, entity, gets[i]);

        UnitSourceSetDefinition<TComponent>[] sets = GetSets();
        for (int i = 0; i < sets.Length; i++)
            AddSet(table, entityManager, entity, sets[i]);
    }

    protected abstract void Define(UnitSourceDefinitionBuilder<TComponent> builder);

    private UnitSourceGetDefinition<TComponent>[] GetGets()
    {
        EnsureDefinitions();
        return _gets;
    }

    private UnitSourceSetDefinition<TComponent>[] GetSets()
    {
        EnsureDefinitions();
        return _sets;
    }

    private void EnsureDefinitions()
    {
        if (_gets != null)
            return;

        UnitSourceDefinitionBuilder<TComponent> builder = new();
        Define(builder);
        _gets = builder.BuildGets();
        _sets = builder.BuildSets();
    }

    private static void AddGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity entity,
        UnitSourceGetDefinition<TComponent> definition)
    {
        table.AddGet(new UnitSourceGet(
            definition.Key,
            definition.ReturnType,
            definition.Parameters,
            parameters =>
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
                    return UnitValue.None;

                TComponent component = entityManager.GetComponentData<TComponent>(entity);
                return definition.Invoke(in component, parameters);
            }));
    }

    private static void AddSet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity entity,
        UnitSourceSetDefinition<TComponent> definition)
    {
        if (definition.RequiresKey)
        {
            table.AddSet(new UnitSourceSet(
                definition.Key,
                definition.Parameters,
                (string key, UnitValue value) =>
                {
                    if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
                        return false;

                    TComponent component = entityManager.GetComponentData<TComponent>(entity);
                    if (!definition.KeyedInvoke(ref component, key, value))
                        return false;

                    entityManager.SetComponentData(entity, component);
                    return true;
                }));
            return;
        }

        table.AddSet(new UnitSourceSet(
            definition.Key,
            definition.Parameters,
            parameters =>
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
                    return false;

                TComponent component = entityManager.GetComponentData<TComponent>(entity);
                if (!definition.Invoke(ref component, parameters[0]))
                    return false;

                entityManager.SetComponentData(entity, component);
                return true;
            }));
    }
}

public abstract class UnitManagedComponentSource<TComponent> : UnitComponentSource
    where TComponent : class, IComponentData
{
    private UnitSourceGetDefinition<TComponent>[] _gets;
    private UnitSourceSetDefinition<TComponent>[] _sets;

    public sealed override Type ComponentType => typeof(TComponent);

    public sealed override void Describe(UnitSourceSchemaBuilder schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        UnitSourceGetDefinition<TComponent>[] gets = GetGets();
        for (int i = 0; i < gets.Length; i++)
            schema.AddGet(gets[i].Key, ComponentType, gets[i].ReturnType, gets[i].Parameters);

        UnitSourceSetDefinition<TComponent>[] sets = GetSets();
        for (int i = 0; i < sets.Length; i++)
            schema.AddSet(sets[i].Key, ComponentType, sets[i].Parameters, sets[i].RequiresKey);
    }

    public sealed override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));

        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
            return;

        UnitSourceGetDefinition<TComponent>[] gets = GetGets();
        for (int i = 0; i < gets.Length; i++)
            AddGet(table, entityManager, entity, gets[i]);

        UnitSourceSetDefinition<TComponent>[] sets = GetSets();
        for (int i = 0; i < sets.Length; i++)
            AddSet(table, entityManager, entity, sets[i]);
    }

    protected abstract void Define(UnitSourceDefinitionBuilder<TComponent> builder);

    private UnitSourceGetDefinition<TComponent>[] GetGets()
    {
        EnsureDefinitions();
        return _gets;
    }

    private UnitSourceSetDefinition<TComponent>[] GetSets()
    {
        EnsureDefinitions();
        return _sets;
    }

    private void EnsureDefinitions()
    {
        if (_gets != null)
            return;

        UnitSourceDefinitionBuilder<TComponent> builder = new();
        Define(builder);
        _gets = builder.BuildGets();
        _sets = builder.BuildSets();
    }

    private static void AddGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity entity,
        UnitSourceGetDefinition<TComponent> definition)
    {
        table.AddGet(new UnitSourceGet(
            definition.Key,
            definition.ReturnType,
            definition.Parameters,
            parameters =>
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
                    return UnitValue.None;

                TComponent component = entityManager.GetComponentObject<TComponent>(entity);
                return definition.Invoke(in component, parameters);
            }));
    }

    private static void AddSet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity entity,
        UnitSourceSetDefinition<TComponent> definition)
    {
        if (definition.RequiresKey)
        {
            table.AddSet(new UnitSourceSet(
                definition.Key,
                definition.Parameters,
                (string key, UnitValue value) =>
                {
                    if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
                        return false;

                    TComponent component = entityManager.GetComponentObject<TComponent>(entity);
                    return definition.KeyedInvoke(ref component, key, value);
                }));
            return;
        }

        table.AddSet(new UnitSourceSet(
            definition.Key,
            definition.Parameters,
            parameters =>
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<TComponent>(entity))
                    return false;

                TComponent component = entityManager.GetComponentObject<TComponent>(entity);
                if (!definition.Invoke(ref component, parameters[0]))
                    return false;

                // Managed components are reference objects already owned by the entity.
                // Source setters modify that object in place, so no ECS write-back is needed.
                return true;
            }));
    }
}

internal sealed class UnitSourceGetDefinition<TComponent>
{
    public UnitSourceGetDefinition(
        string key,
        UnitValueCategory returnType,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        ComponentGetter<TComponent> invoke)
    {
        Key = key;
        ReturnType = returnType;
        Parameters = parameters ?? Array.Empty<ComparatorParameterDefinition>();
        Invoke = invoke;
    }

    public string Key { get; }
    public UnitValueCategory ReturnType { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    public ComponentGetter<TComponent> Invoke { get; }
}

internal sealed class UnitSourceSetDefinition<TComponent>
{
    public UnitSourceSetDefinition(
        string key,
        UnitValueCategory valueType,
        ComponentValueSetter<TComponent> invoke)
    {
        Key = key;
        Parameters = new[] { new ComparatorParameterDefinition("Value", valueType) };
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public UnitSourceSetDefinition(
        string key,
        UnitValueCategory valueType,
        ComponentKeyedValueSetter<TComponent> keyedInvoke)
    {
        Key = key;
        Parameters = new[] { new ComparatorParameterDefinition("Value", valueType) };
        RequiresKey = true;
        KeyedInvoke = keyedInvoke ?? throw new ArgumentNullException(nameof(keyedInvoke));
    }

    public string Key { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    public ComponentValueSetter<TComponent> Invoke { get; }
    public ComponentKeyedValueSetter<TComponent> KeyedInvoke { get; }
    public bool RequiresKey { get; }
}
