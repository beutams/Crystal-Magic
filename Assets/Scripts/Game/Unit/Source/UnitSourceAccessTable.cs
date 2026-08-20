using System;
using System.Collections.Generic;

public delegate UnitValue UnitSourceGetHandler(UnitValue[] parameters);
public delegate bool UnitSourceSetHandler(UnitValue[] parameters);
public delegate bool UnitSourceKeyedSetHandler(string key, UnitValue value);

public sealed class UnitSourceGet : IParameterizedUnitValueGetter
{
    public UnitSourceGet(
        string key,
        UnitValueCategory returnType,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        UnitSourceGetHandler invoke)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Unit source get key cannot be empty.", nameof(key));

        if (returnType == UnitValueCategory.None)
            throw new ArgumentOutOfRangeException(nameof(returnType));

        Key = key;
        ReturnType = returnType;
        Parameters = parameters ?? Array.Empty<ComparatorParameterDefinition>();
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public string Key { get; }
    public UnitValueCategory ReturnType { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    public UnitSourceGetHandler Invoke { get; }

    public bool TryGet(UnitValue[] parameters, out UnitValue value)
    {
        UnitValue[] input = parameters ?? Array.Empty<UnitValue>();
        if (!HasValidParameters(input))
        {
            value = UnitValue.None;
            return false;
        }

        value = Invoke(input);
        return value.Category != UnitValueCategory.None &&
               (ReturnType == UnitValueCategory.Any || value.Category == ReturnType);
    }

    private bool HasValidParameters(UnitValue[] parameters)
    {
        if (parameters.Length != Parameters.Count)
            return false;

        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].Accepts(parameters[i].Category))
                return false;
        }

        return true;
    }
}

public sealed class UnitSourceSet
{
    public UnitSourceSet(
        string key,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        UnitSourceSetHandler invoke)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Unit source set key cannot be empty.", nameof(key));

        ValidateParameters(parameters);

        Key = key;
        Parameters = parameters;
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public UnitSourceSet(
        string key,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        UnitSourceKeyedSetHandler invoke)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Unit source set key cannot be empty.", nameof(key));

        ValidateParameters(parameters);

        Key = key;
        Parameters = parameters;
        RequiresKey = true;
        KeyedInvoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public string Key { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    public UnitSourceSetHandler Invoke { get; }
    public UnitSourceKeyedSetHandler KeyedInvoke { get; }
    public bool RequiresKey { get; }

    public bool TrySet(UnitValue[] parameters)
    {
        if (RequiresKey)
            return false;

        UnitValue[] input = parameters ?? Array.Empty<UnitValue>();
        if (input.Length != Parameters.Count)
            return false;

        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].Accepts(input[i].Category))
                return false;
        }

        return Invoke(input);
    }

    public bool TrySet(string key, UnitValue value)
    {
        return RequiresKey &&
               !string.IsNullOrWhiteSpace(key) &&
               Parameters[0].Accepts(value.Category) &&
               KeyedInvoke(key, value);
    }

    private static void ValidateParameters(IReadOnlyList<ComparatorParameterDefinition> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            throw new ArgumentException("Unit source setters must accept at least one value parameter.", nameof(parameters));

        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Category == UnitValueCategory.None)
                throw new ArgumentException("Unit source setter parameters must have a value category.", nameof(parameters));
        }
    }
}

// Per-unit table. Graphs bind a key to one of these functions during initialization.
public sealed class UnitSourceAccessTable : IComparatorValueResolver
{
    private readonly Dictionary<string, UnitSourceGet> _gets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitSourceSet> _sets = new(StringComparer.Ordinal);

    public IEnumerable<UnitSourceGet> Gets => _gets.Values;
    public IEnumerable<UnitSourceSet> Sets => _sets.Values;

    internal void AddGet(UnitSourceGet sourceGet)
    {
        if (sourceGet == null)
            throw new ArgumentNullException(nameof(sourceGet));

        if (!_gets.TryAdd(sourceGet.Key, sourceGet))
            throw new InvalidOperationException($"Unit source get is already registered: {sourceGet.Key}");
    }

    internal void AddSet(UnitSourceSet sourceSet)
    {
        if (sourceSet == null)
            throw new ArgumentNullException(nameof(sourceSet));

        if (!_sets.TryAdd(sourceSet.Key, sourceSet))
            throw new InvalidOperationException($"Unit source set is already registered: {sourceSet.Key}");
    }

    public bool TryGet(string key, UnitValue[] parameters, out UnitValue value)
    {
        if (_gets.TryGetValue(key ?? string.Empty, out UnitSourceGet sourceGet))
            return sourceGet.TryGet(parameters, out value);

        value = UnitValue.None;
        return false;
    }

    public bool TrySet(string key, UnitValue[] parameters)
    {
        return _sets.TryGetValue(key ?? string.Empty, out UnitSourceSet sourceSet) &&
               sourceSet.TrySet(parameters);
    }

    public bool TryGetDefinition(string key, out UnitSourceGet sourceGet)
    {
        return _gets.TryGetValue(key ?? string.Empty, out sourceGet);
    }

    public bool TryGetDefinition(string key, out UnitSourceSet sourceSet)
    {
        return _sets.TryGetValue(key ?? string.Empty, out sourceSet);
    }

    bool IComparatorValueResolver.TryGet(string key, out IParameterizedUnitValueGetter getter)
    {
        if (_gets.TryGetValue(key ?? string.Empty, out UnitSourceGet sourceGet))
        {
            getter = sourceGet;
            return true;
        }

        getter = null;
        return false;
    }
}
