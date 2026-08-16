using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public readonly struct SourceContext
{
    public SourceContext(
        Entity entity,
        EntityManager entityManager,
        Entity originEntity,
        bool hasOriginEntity,
        int sourceParam = -1,
        GameObject unitPrefab = null,
        CrystalMagic.Game.Data.UnitData unitData = null,
        bool hasRuntimeEntity = true)
    {
        Entity = entity;
        EntityManager = entityManager;
        OriginEntity = originEntity;
        HasOriginEntity = hasOriginEntity;
        SourceParam = sourceParam;
        UnitPrefab = unitPrefab;
        UnitData = unitData;
        HasRuntimeEntity = hasRuntimeEntity;
    }

    public Entity Entity { get; }
    public EntityManager EntityManager { get; }
    public Entity OriginEntity { get; }
    public bool HasOriginEntity { get; }
    public int SourceParam { get; }
    public GameObject UnitPrefab { get; }
    public CrystalMagic.Game.Data.UnitData UnitData { get; }
    public bool HasRuntimeEntity { get; }
}

// Temporary bridge for existing callers. New conditions use IComparatorValueResolver.
public interface ISource
{
    float GetValue();

    void Init(Entity entity, EntityManager entityManager) { }

    void Init(SourceContext context)
    {
        Init(context.Entity, context.EntityManager);
    }

    bool CanUse()
    {
        return true;
    }
}

public enum UnitValueType
{
    None,
    Bool,
    Int,
    Float,
    Float2,
    Float3,
    Entity,
    String,
}

public enum UnitValueCategory
{
    None,
    Any,
    Bool,
    Number,
    Float2,
    Float3,
    Entity,
    String,
}

[Serializable]
public struct UnitValue
{
    public UnitValueType Type;
    public bool Bool;
    public int Int;
    public float Float;
    public float2 Float2;
    public float3 Float3;
    public Entity Entity;
    public string String;

    public UnitValueCategory Category => Type switch
    {
        UnitValueType.Bool => UnitValueCategory.Bool,
        UnitValueType.Int => UnitValueCategory.Number,
        UnitValueType.Float => UnitValueCategory.Number,
        UnitValueType.Float2 => UnitValueCategory.Float2,
        UnitValueType.Float3 => UnitValueCategory.Float3,
        UnitValueType.Entity => UnitValueCategory.Entity,
        UnitValueType.String => UnitValueCategory.String,
        _ => UnitValueCategory.None,
    };

    public static UnitValue None => default;

    public static UnitValue FromBool(bool value) => new()
    {
        Type = UnitValueType.Bool,
        Bool = value,
    };

    public static UnitValue FromInt(int value) => new()
    {
        Type = UnitValueType.Int,
        Int = value,
    };

    public static UnitValue FromFloat(float value) => new()
    {
        Type = UnitValueType.Float,
        Float = value,
    };

    public static UnitValue FromFloat2(float2 value) => new()
    {
        Type = UnitValueType.Float2,
        Float2 = value,
    };

    public static UnitValue FromFloat3(float3 value) => new()
    {
        Type = UnitValueType.Float3,
        Float3 = value,
    };

    public static UnitValue FromEntity(Entity value) => new()
    {
        Type = UnitValueType.Entity,
        Entity = value,
    };

    public static UnitValue FromString(string value) => new()
    {
        Type = UnitValueType.String,
        String = value ?? string.Empty,
    };

    public bool TryGetNumber(out float value)
    {
        switch (Type)
        {
            case UnitValueType.Int:
                value = Int;
                return true;
            case UnitValueType.Float:
                value = Float;
                return true;
            default:
                value = 0f;
                return false;
        }
    }

    public bool TryGetFloat3(out float3 value)
    {
        if (Type == UnitValueType.Float3)
        {
            value = Float3;
            return true;
        }

        value = float3.zero;
        return false;
    }

    public bool TryGetFloat2(out float2 value)
    {
        if (Type == UnitValueType.Float2)
        {
            value = Float2;
            return true;
        }

        value = float2.zero;
        return false;
    }

    public bool TryGetString(out string value)
    {
        if (Type == UnitValueType.String)
        {
            value = String ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool EqualsValue(in UnitValue other)
    {
        if (TryGetNumber(out float leftNumber) && other.TryGetNumber(out float rightNumber))
            return math.abs(leftNumber - rightNumber) <= 0.0001f;

        if (Type != other.Type)
            return false;

        return Type switch
        {
            UnitValueType.None => true,
            UnitValueType.Bool => Bool == other.Bool,
            UnitValueType.Float2 => math.all(Float2 == other.Float2),
            UnitValueType.Float3 => math.all(Float3 == other.Float3),
            UnitValueType.Entity => Entity == other.Entity,
            UnitValueType.String => string.Equals(String, other.String, StringComparison.Ordinal),
            _ => false,
        };
    }
}

public readonly struct ComparatorParameterDefinition
{
    public ComparatorParameterDefinition(string name, UnitValueCategory category)
    {
        Name = name ?? string.Empty;
        Category = category;
    }

    public string Name { get; }
    public UnitValueCategory Category { get; }

    public bool Accepts(UnitValueCategory valueCategory)
    {
        return valueCategory != UnitValueCategory.None &&
               (Category == UnitValueCategory.Any || Category == valueCategory);
    }
}

public interface IParameterizedUnitValueGetter
{
    UnitValueCategory ReturnType { get; }
    IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    bool TryGet(UnitValue[] parameters, out UnitValue value);
}

// Implemented by the unit source access table and any later variable resolver.
public interface IComparatorValueResolver
{
    bool TryGet(string key, out IParameterizedUnitValueGetter getter);
}
