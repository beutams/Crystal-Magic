using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class GeneratedFactory<TKey, TValue>
{
    private readonly Dictionary<TKey, Func<TValue>> _factories = new();

    protected void Register(TKey key, Func<TValue> factory)
    {
        _factories[key] = factory;
    }

    protected TValue Create(TKey key)
    {
        if (_factories.TryGetValue(key, out Func<TValue> factory))
        {
            return factory();
        }

        Debug.LogError($"[{GetType().Name}] Unregistered key: {key}");
        return default;
    }

    public int Count => _factories.Count;
}

public abstract class GeneratedFactory<TKey, TInput, TValue>
{
    private readonly Dictionary<TKey, Func<TInput, TValue>> _factories = new();

    protected void Register(TKey key, Func<TInput, TValue> factory)
    {
        _factories[key] = factory;
    }

    protected TValue Create(TKey key, TInput input)
    {
        if (_factories.TryGetValue(key, out Func<TInput, TValue> factory))
        {
            return factory(input);
        }

        Debug.LogError($"[{GetType().Name}] Unregistered key: {key}");
        return default;
    }

    public int Count => _factories.Count;
}
