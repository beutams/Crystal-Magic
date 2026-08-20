using System;
using System.Collections.Generic;

public readonly struct UnitSourceGetSchemaEntry
{
    public UnitSourceGetSchemaEntry(
        string key,
        Type componentType,
        UnitValueCategory returnType,
        IReadOnlyList<ComparatorParameterDefinition> parameters)
    {
        Key = key;
        ComponentType = componentType;
        ReturnType = returnType;
        Parameters = parameters ?? Array.Empty<ComparatorParameterDefinition>();
    }

    public string Key { get; }
    public Type ComponentType { get; }
    public UnitValueCategory ReturnType { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
}

public readonly struct UnitSourceSetSchemaEntry
{
    public UnitSourceSetSchemaEntry(
        string key,
        Type componentType,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        bool requiresKey)
    {
        Key = key;
        ComponentType = componentType;
        Parameters = parameters ?? Array.Empty<ComparatorParameterDefinition>();
        RequiresKey = requiresKey;
    }

    public string Key { get; }
    public Type ComponentType { get; }
    public IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    public bool RequiresKey { get; }
}

public sealed class UnitSourceSchema
{
    private readonly Dictionary<string, UnitSourceGetSchemaEntry> _gets;
    private readonly Dictionary<string, UnitSourceSetSchemaEntry> _sets;

    internal UnitSourceSchema(
        Dictionary<string, UnitSourceGetSchemaEntry> gets,
        Dictionary<string, UnitSourceSetSchemaEntry> sets)
    {
        _gets = gets;
        _sets = sets;
    }

    public IEnumerable<UnitSourceGetSchemaEntry> Gets => _gets.Values;
    public IEnumerable<UnitSourceSetSchemaEntry> Sets => _sets.Values;

    public bool TryGet(string key, out UnitSourceGetSchemaEntry entry)
    {
        return _gets.TryGetValue(key ?? string.Empty, out entry);
    }

    public bool TryGet(string key, out UnitSourceSetSchemaEntry entry)
    {
        return _sets.TryGetValue(key ?? string.Empty, out entry);
    }
}

public sealed class UnitSourceSchemaBuilder
{
    private readonly Dictionary<string, UnitSourceGetSchemaEntry> _gets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitSourceSetSchemaEntry> _sets = new(StringComparer.Ordinal);

    public void AddGet(
        string key,
        Type componentType,
        UnitValueCategory returnType,
        IReadOnlyList<ComparatorParameterDefinition> parameters)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Unit source get key cannot be empty.", nameof(key));

        if (componentType == null)
            throw new ArgumentNullException(nameof(componentType));

        if (returnType == UnitValueCategory.None)
            throw new ArgumentOutOfRangeException(nameof(returnType));

        if (!_gets.TryAdd(key, new UnitSourceGetSchemaEntry(key, componentType, returnType, parameters)))
            throw new InvalidOperationException($"Unit source get is already defined: {key}");
    }

    public void AddSet(
        string key,
        Type componentType,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        bool requiresKey = false)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Unit source set key cannot be empty.", nameof(key));

        if (componentType == null)
            throw new ArgumentNullException(nameof(componentType));

        if (parameters == null || parameters.Count == 0)
            throw new ArgumentException("Unit source setters must define at least one value parameter.", nameof(parameters));

        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Category == UnitValueCategory.None)
                throw new ArgumentException("Unit source setter parameters must have a value category.", nameof(parameters));
        }

        if (!_sets.TryAdd(key, new UnitSourceSetSchemaEntry(key, componentType, parameters, requiresKey)))
            throw new InvalidOperationException($"Unit source set is already defined: {key}");
    }

    public UnitSourceSchema Build()
    {
        return new UnitSourceSchema(
            new Dictionary<string, UnitSourceGetSchemaEntry>(_gets, StringComparer.Ordinal),
            new Dictionary<string, UnitSourceSetSchemaEntry>(_sets, StringComparer.Ordinal));
    }
}
