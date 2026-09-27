using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using System.Threading;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using UnityEngine;

public readonly struct EffectDataListId : IEquatable<EffectDataListId>
{
    public EffectDataListId(int value)
    {
        Value = value;
    }

    public readonly int Value;

    public bool IsValid => Value > 0;

    public bool Equals(EffectDataListId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is EffectDataListId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(EffectDataListId left, EffectDataListId right) => left.Equals(right);

    public static bool operator !=(EffectDataListId left, EffectDataListId right) => !left.Equals(right);
}

public sealed class EffectDataList : IPoolable
{
    public EffectDataList()
    {
        Effects = Array.Empty<EffectData>();
    }

    public EffectDataList(EffectData[] effects)
    {
        Initialize(effects);
    }

    public EffectData[] Effects { get; private set; }
    internal bool IsPoolOwned { get; private set; }

    public void Initialize(EffectData[] effects)
    {
        Effects = effects ?? Array.Empty<EffectData>();
    }

    public void OnGetFromPool()
    {
        Effects = Array.Empty<EffectData>();
        IsPoolOwned = true;
    }

    public void OnReturnToPool()
    {
        Effects = Array.Empty<EffectData>();
        IsPoolOwned = false;
    }
}

public sealed class EffectDataBridgeComponent : IComponentData
{
    public readonly Dictionary<int, EffectDataList> Values = new();
    public readonly HashSet<int> RegistryIds = new();
    public readonly Dictionary<int, EffectManagedContextState> ManagedContexts = new();
    public readonly Dictionary<int, List<ConditionConfig>> ConditionLists = new();
}

public readonly struct EffectManagedContextId : IEquatable<EffectManagedContextId>
{
    public EffectManagedContextId(int value) => Value = value;

    public readonly int Value;

    public bool IsValid => Value > 0;

    public bool Equals(EffectManagedContextId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is EffectManagedContextId other && Equals(other);

    public override int GetHashCode() => Value;
}

public sealed class EffectManagedContextState : IPoolable
{
    public bool HasTarget;
    public GameObject Target;
    public GameObject Origin;
    public Dictionary<int, HashSet<Entity>> PersistentEffectAppliedBuffTargets;

    public void OnGetFromPool()
    {
        Reset();
    }

    public void OnReturnToPool()
    {
        Reset();
    }

    private void Reset()
    {
        HasTarget = false;
        Target = null;
        Origin = null;
        PersistentEffectAppliedBuffTargets = null;
    }
}

public readonly struct ConditionDataListId : IEquatable<ConditionDataListId>
{
    public ConditionDataListId(int value) => Value = value;

    public readonly int Value;

    public bool IsValid => Value > 0;

    public bool Equals(ConditionDataListId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is ConditionDataListId other && Equals(other);

    public override int GetHashCode() => Value;
}

public static class EffectDataListIdAllocator
{
    private static int s_nextId;

    public static EffectDataListId Allocate()
    {
        return new EffectDataListId(Interlocked.Increment(ref s_nextId));
    }
}

public static class EffectManagedContextIdAllocator
{
    private static int s_nextId;

    public static EffectManagedContextId Allocate()
    {
        return new EffectManagedContextId(Interlocked.Increment(ref s_nextId));
    }
}

public static class ConditionDataListIdAllocator
{
    private static int s_nextId;

    public static ConditionDataListId Allocate()
    {
        return new ConditionDataListId(Interlocked.Increment(ref s_nextId));
    }
}
