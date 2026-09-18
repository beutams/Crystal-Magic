using System;
using System.Collections.Generic;
using Unity.Entities;

public sealed class UnitSourceGet : IParameterizedUnitValueGetter
{
    private readonly UnitSourceResolver _resolver;
    private readonly UnitSourceId _sourceId;

    internal UnitSourceGet(
        UnitSourceResolver resolver,
        UnitSourceId sourceId,
        UnitSourceGetSchemaEntry schema)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sourceId = sourceId;
        Key = schema.Key;
        ReturnType = schema.ReturnType;
        Parameters = schema.Parameters;
    }

    public string Key { get; }
    public UnitValueCategory ReturnType { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }

    public bool TryGet(UnitValue[] parameters, out UnitValue value)
    {
        value = UnitValue.None;
        if (!HasValidParameters(parameters) ||
            !UnitSourceArguments.TryCreate(parameters, null, out UnitSourceArguments arguments) ||
            !_resolver.TryGet(_sourceId, in arguments, out UnitSourceValue sourceValue))
        {
            return false;
        }

        value = sourceValue.ToUnitValue();
        return value.Category != UnitValueCategory.None &&
               (ReturnType == UnitValueCategory.Any || value.Category == ReturnType);
    }

    private bool HasValidParameters(UnitValue[] parameters)
    {
        UnitValue[] input = parameters ?? Array.Empty<UnitValue>();
        if (input.Length != Parameters.Count)
            return false;

        for (int index = 0; index < Parameters.Count; index++)
        {
            if (!Parameters[index].Accepts(input[index].Category))
                return false;
        }

        return true;
    }
}

public sealed class UnitSourceSet
{
    private readonly UnitSourceResolver _resolver;
    private readonly UnitSourceId _sourceId;

    internal UnitSourceSet(
        UnitSourceResolver resolver,
        UnitSourceId sourceId,
        UnitSourceSetSchemaEntry schema)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sourceId = sourceId;
        Key = schema.Key;
        Parameters = schema.Parameters;
        RequiresKey = schema.RequiresKey;
    }

    public string Key { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    public bool RequiresKey { get; }

    public bool TrySet(UnitValue[] parameters)
    {
        if (RequiresKey || !HasValidParameters(parameters) ||
            !UnitSourceArguments.TryCreate(parameters, null, out UnitSourceArguments arguments))
        {
            return false;
        }

        return _resolver.TrySet(_sourceId, in arguments);
    }

    public bool TrySet(string key, UnitValue value)
    {
        if (!RequiresKey || string.IsNullOrWhiteSpace(key) ||
            Parameters.Count != 1 || !Parameters[0].Accepts(value.Category) ||
            !UnitSourceArguments.TryCreate(new[] { value }, key, out UnitSourceArguments arguments))
        {
            return false;
        }

        return _resolver.TrySet(_sourceId, in arguments);
    }

    private bool HasValidParameters(UnitValue[] parameters)
    {
        UnitValue[] input = parameters ?? Array.Empty<UnitValue>();
        if (input.Length != Parameters.Count)
            return false;

        for (int index = 0; index < Parameters.Count; index++)
        {
            if (!Parameters[index].Accepts(input[index].Category))
                return false;
        }

        return true;
    }
}

// Managed graph runtimes keep only their entity, world access, and the current generated dispatcher.
// There is no per-unit source dictionary, binding callback, or mirrored component state.
public sealed class UnitSourceResolver : IComparatorValueResolver
{
    private EntityManager _entityManager;
    private UnitSourceDispatcher _dispatcher;
    private bool _hasDispatcher;

    public UnitSourceResolver(Entity entity)
    {
        Entity = entity;
    }

    public Entity Entity { get; private set; }
    public UnitSourceDispatcher Dispatcher => _dispatcher;

    public void Update(
        Entity entity,
        EntityManager entityManager,
        in UnitSourceDispatcher dispatcher)
    {
        Entity = entity;
        _entityManager = entityManager;
        _dispatcher = dispatcher;
        _hasDispatcher = true;
    }

    public bool TryGet(UnitSourceId sourceId, in UnitSourceArguments arguments, out UnitSourceValue value)
    {
        value = default;
        if (!_hasDispatcher)
            return false;

        if (_dispatcher.TryGet(Entity, sourceId, in arguments, out value))
            return true;

        bool success = false;
        _dispatcher.InvokeManagedGet(
            _entityManager,
            sourceId,
            Entity,
            in arguments,
            ref success,
            ref value);
        return success;
    }

    public bool TrySet(UnitSourceId sourceId, in UnitSourceArguments arguments)
    {
        if (!_hasDispatcher)
            return false;

        if (_dispatcher.TrySet(Entity, sourceId, in arguments))
            return true;

        bool success = false;
        _dispatcher.InvokeManagedSet(
            _entityManager,
            sourceId,
            Entity,
            in arguments,
            ref success);
        return success;
    }

    public bool TryGet(string key, UnitValue[] parameters, out UnitValue value)
    {
        value = UnitValue.None;
        return TryGetDefinition(key, out UnitSourceGet sourceGet) &&
               sourceGet.TryGet(parameters, out value);
    }

    public bool TrySet(string key, UnitValue[] parameters)
    {
        return TryGetDefinition(key, out UnitSourceSet sourceSet) &&
               sourceSet.TrySet(parameters);
    }

    public bool TryGetInteraction(string key, out InteractionRequestSnapshot request)
    {
        request = default;
        if (!_hasDispatcher ||
            !string.Equals(key, UnitComponentSourceRegistry.InteractionRequestKey, StringComparison.Ordinal))
        {
            return false;
        }

        bool success = false;
        _dispatcher.InvokeManagedInteraction(_entityManager, ref success, ref request);
        return success;
    }

    public bool TryGetDefinition(string key, out UnitSourceGet sourceGet)
    {
        if (UnitComponentSourceRegistry.TryGetGet(key, out UnitSourceId sourceId, out UnitSourceGetSchemaEntry schema))
        {
            sourceGet = new UnitSourceGet(this, sourceId, schema);
            return true;
        }

        sourceGet = null;
        return false;
    }

    public bool TryGetDefinition(string key, out UnitSourceSet sourceSet)
    {
        if (UnitComponentSourceRegistry.TryGetSet(key, out UnitSourceId sourceId, out UnitSourceSetSchemaEntry schema))
        {
            sourceSet = new UnitSourceSet(this, sourceId, schema);
            return true;
        }

        sourceSet = null;
        return false;
    }

    public bool TryGetInteractionDefinition(string key, out InteractionRequestSourceGet sourceGet)
    {
        if (!string.Equals(key, UnitComponentSourceRegistry.InteractionRequestKey, StringComparison.Ordinal))
        {
            sourceGet = null;
            return false;
        }

        sourceGet = new InteractionRequestSourceGet(key, TryGetInteractionCandidate);
        return true;
    }

    bool IComparatorValueResolver.TryGet(string key, out IParameterizedUnitValueGetter getter)
    {
        if (TryGetDefinition(key, out UnitSourceGet sourceGet))
        {
            getter = sourceGet;
            return true;
        }

        getter = null;
        return false;
    }

    private bool TryGetInteractionCandidate(out InteractionRequestSnapshot request)
    {
        return TryGetInteraction(UnitComponentSourceRegistry.InteractionRequestKey, out request);
    }
}
