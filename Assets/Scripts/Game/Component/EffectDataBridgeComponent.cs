using System;
using System.Collections.Generic;
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

public sealed class EffectDataList
{
    public EffectDataList(EffectData[] effects)
    {
        Effects = effects ?? Array.Empty<EffectData>();
    }

    public EffectData[] Effects { get; }
}

public sealed class EffectDataBridgeComponent : IComponentData
{
    public readonly Dictionary<int, EffectDataList> Values = new();
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

public sealed class EffectManagedContextState
{
    public bool HasTarget;
    public GameObject Target;
    public GameObject Origin;
    public Dictionary<int, HashSet<Entity>> PersistentEffectAppliedBuffTargets;
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
