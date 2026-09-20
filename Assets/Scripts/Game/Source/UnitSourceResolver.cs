using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

public sealed class UnitSourceGet : IParameterizedUnitValueGetter
{
    private readonly UnitSourceResolver _resolver;
    private readonly UnitSourceId _sourceId;
    private readonly UnitSourceTarget _sourceTarget;

    internal UnitSourceGet(
        UnitSourceResolver resolver,
        UnitSourceId sourceId,
        UnitSourceTarget sourceTarget,
        UnitSourceGetSchemaEntry schema)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sourceId = sourceId;
        _sourceTarget = sourceTarget;
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
            !_resolver.TryGet(_sourceId, _sourceTarget, in arguments, out UnitSourceValue sourceValue))
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
    private readonly UnitSourceTarget _sourceTarget;

    internal UnitSourceSet(
        UnitSourceResolver resolver,
        UnitSourceId sourceId,
        UnitSourceTarget sourceTarget,
        UnitSourceSetSchemaEntry schema)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sourceId = sourceId;
        _sourceTarget = sourceTarget;
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

        return _resolver.TrySet(_sourceId, _sourceTarget, in arguments);
    }

    public bool TrySet(string key, UnitValue value)
    {
        if (!RequiresKey || string.IsNullOrWhiteSpace(key) ||
            Parameters.Count != 1 || !Parameters[0].Accepts(value.Category) ||
            !UnitSourceArguments.TryCreate(new[] { value }, key, out UnitSourceArguments arguments))
        {
            return false;
        }

        return _resolver.TrySet(_sourceId, _sourceTarget, in arguments);
    }

    public bool TrySet(in UnitSourceArguments arguments)
    {
        return !RequiresKey && HasValidParameters(in arguments) &&
               _resolver.TrySet(_sourceId, _sourceTarget, in arguments);
    }

    public bool TrySet(string key, in UnitSourceValue value)
    {
        UnitSourceArguments arguments = default;
        if (!RequiresKey || string.IsNullOrWhiteSpace(key) ||
            Parameters.Count != 1 || !Parameters[0].Accepts(value.Category) ||
            arguments.Key.CopyFrom(key) != CopyError.None)
        {
            return false;
        }

        arguments.HasKey = 1;
        arguments.Values.Add(value);
        return _resolver.TrySet(_sourceId, _sourceTarget, in arguments);
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

    private bool HasValidParameters(in UnitSourceArguments arguments)
    {
        if (arguments.Count != Parameters.Count)
            return false;

        for (int index = 0; index < Parameters.Count; index++)
        {
            if (!arguments.TryGet(index, out UnitSourceValue value) ||
                !Parameters[index].Accepts(value.Category))
            {
                return false;
            }
        }

        return true;
    }
}

// Managed graph runtimes keep their Self/Other entity context and the current generated dispatcher.
// There is no per-unit source dictionary, binding callback, or mirrored component state.
public sealed class UnitSourceResolver : IComparatorValueResolver
{
    private UnitSourceDispatcher _dispatcher;
    private UnitSourceContext _context;
    private bool _hasDispatcher;

    public UnitSourceResolver(Entity entity)
    {
        _context = new UnitSourceContext(entity);
    }

    public Entity Entity => _context.Self;
    public Entity OtherEntity => _context.Other;
    public UnitSourceContext Context => _context;
    public UnitSourceDispatcher Dispatcher => _dispatcher;

    public bool TryGetContext(out UnitSourceContext context, out UnitSourceDispatcher dispatcher)
    {
        context = _context;
        dispatcher = _dispatcher;
        return _hasDispatcher;
    }

    public void Update(Entity entity, in UnitSourceDispatcher dispatcher)
    {
        Update(entity, Entity.Null, in dispatcher);
    }

    public void Update(Entity self, Entity other, in UnitSourceDispatcher dispatcher)
    {
        _context = new UnitSourceContext(self, other);
        _dispatcher = dispatcher;
        _hasDispatcher = true;
    }

    public bool TryGet(UnitSourceId sourceId, in UnitSourceArguments arguments, out UnitSourceValue value)
    {
        return TryGet(sourceId, UnitSourceTarget.Self, in arguments, out value);
    }

    public bool TryGet(
        UnitSourceId sourceId,
        UnitSourceTarget sourceTarget,
        in UnitSourceArguments arguments,
        out UnitSourceValue value)
    {
        value = default;
        return _hasDispatcher &&
               _dispatcher.TryGet(_context.Resolve(sourceTarget), sourceId, in arguments, out value);
    }

    public bool TrySet(UnitSourceId sourceId, in UnitSourceArguments arguments)
    {
        return TrySet(sourceId, UnitSourceTarget.Self, in arguments);
    }

    public bool TrySet(
        UnitSourceId sourceId,
        UnitSourceTarget sourceTarget,
        in UnitSourceArguments arguments)
    {
        return _hasDispatcher &&
               _dispatcher.TrySet(_context.Resolve(sourceTarget), sourceId, in arguments);
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

        return _dispatcher.TryGetInteraction(out request);
    }

    public bool TryGetDefinition(string key, out UnitSourceGet sourceGet)
    {
        return TryGetDefinition(key, UnitSourceTarget.Self, out sourceGet);
    }

    public bool TryGetDefinition(
        string key,
        UnitSourceTarget sourceTarget,
        out UnitSourceGet sourceGet)
    {
        if (UnitComponentSourceRegistry.TryGetGet(key, out UnitSourceId sourceId, out UnitSourceGetSchemaEntry schema))
        {
            sourceGet = new UnitSourceGet(this, sourceId, sourceTarget, schema);
            return true;
        }

        sourceGet = null;
        return false;
    }

    public bool TryGetDefinition(string key, out UnitSourceSet sourceSet)
    {
        return TryGetDefinition(key, UnitSourceTarget.Self, out sourceSet);
    }

    public bool TryGetDefinition(
        string key,
        UnitSourceTarget sourceTarget,
        out UnitSourceSet sourceSet)
    {
        if (UnitComponentSourceRegistry.TryGetSet(key, out UnitSourceId sourceId, out UnitSourceSetSchemaEntry schema))
        {
            sourceSet = new UnitSourceSet(this, sourceId, sourceTarget, schema);
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
