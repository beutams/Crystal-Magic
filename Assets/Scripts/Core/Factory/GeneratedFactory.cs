using System;
using System.Collections.Generic;
using UnityEngine;

public class GeneratedFactory<TKey, TValue>
{
    private readonly Dictionary<TKey, Func<TValue>> _factories;

    public GeneratedFactory()
        : this(null)
    {
    }

    public GeneratedFactory(IEqualityComparer<TKey> comparer)
    {
        _factories = comparer == null
            ? new Dictionary<TKey, Func<TValue>>()
            : new Dictionary<TKey, Func<TValue>>(comparer);
    }

    public void Register(TKey key, Func<TValue> factory)
    {
        if (factory == null)
        {
            Debug.LogError($"[{GetType().Name}] Cannot register null factory for key: {key}");
            return;
        }

        _factories[key] = factory;
    }

    public void Register<TConcrete>(TKey key)
        where TConcrete : TValue, new()
    {
        Register(key, static () => new TConcrete());
    }

    public bool TryCreate(TKey key, out TValue value)
    {
        if (_factories.TryGetValue(key, out Func<TValue> factory))
        {
            value = factory();
            return true;
        }

        value = default;
        return false;
    }

    public TValue Create(TKey key)
    {
        if (TryCreate(key, out TValue value))
        {
            return value;
        }

        Debug.LogError($"[{GetType().Name}] Unregistered key: {key}");
        return default;
    }

    public bool Contains(TKey key) => _factories.ContainsKey(key);

    public void Clear() => _factories.Clear();

    public int Count => _factories.Count;
}

public class GeneratedFactory<TKey, TInput, TValue>
{
    private readonly Dictionary<TKey, Func<TInput, TValue>> _factories;

    public GeneratedFactory()
        : this(null)
    {
    }

    public GeneratedFactory(IEqualityComparer<TKey> comparer)
    {
        _factories = comparer == null
            ? new Dictionary<TKey, Func<TInput, TValue>>()
            : new Dictionary<TKey, Func<TInput, TValue>>(comparer);
    }

    public void Register(TKey key, Func<TInput, TValue> factory)
    {
        if (factory == null)
        {
            Debug.LogError($"[{GetType().Name}] Cannot register null factory for key: {key}");
            return;
        }

        _factories[key] = factory;
    }

    public bool TryCreate(TKey key, TInput input, out TValue value)
    {
        if (_factories.TryGetValue(key, out Func<TInput, TValue> factory))
        {
            value = factory(input);
            return true;
        }

        value = default;
        return false;
    }

    public TValue Create(TKey key, TInput input)
    {
        if (TryCreate(key, input, out TValue value))
        {
            return value;
        }

        Debug.LogError($"[{GetType().Name}] Unregistered key: {key}");
        return default;
    }

    public bool Contains(TKey key) => _factories.ContainsKey(key);

    public void Clear() => _factories.Clear();

    public int Count => _factories.Count;
}
