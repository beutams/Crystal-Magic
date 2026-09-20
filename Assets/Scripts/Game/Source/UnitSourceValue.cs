using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum UnitSourceTarget : byte
{
    Self = 0,
    Other = 1,
}

public struct UnitSourceContext
{
    public Entity Self;
    public Entity Other;

    public UnitSourceContext(Entity self, Entity other = default)
    {
        Self = self;
        Other = other;
    }

    public Entity Resolve(UnitSourceTarget target)
    {
        return target == UnitSourceTarget.Other ? Other : Self;
    }
}

public readonly struct UnitSourceAccessContext
{
    public readonly Entity TargetEntity;
    public readonly Entity GlobalEntity;

    public UnitSourceAccessContext(Entity targetEntity, Entity globalEntity)
    {
        TargetEntity = targetEntity;
        GlobalEntity = globalEntity;
    }
}

public struct UnitSourceValue
{
    public UnitValueType Type;
    public byte Bool;
    public int Int;
    public float Float;
    public float2 Float2;
    public float3 Float3;
    public Entity Entity;
    public FixedString128Bytes String;

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

    public static UnitSourceValue None => default;
    public static UnitSourceValue FromBool(bool value) => new() { Type = UnitValueType.Bool, Bool = value ? (byte)1 : (byte)0 };
    public static UnitSourceValue FromInt(int value) => new() { Type = UnitValueType.Int, Int = value };
    public static UnitSourceValue FromFloat(float value) => new() { Type = UnitValueType.Float, Float = value };
    public static UnitSourceValue FromFloat2(float2 value) => new() { Type = UnitValueType.Float2, Float2 = value };
    public static UnitSourceValue FromFloat3(float3 value) => new() { Type = UnitValueType.Float3, Float3 = value };
    public static UnitSourceValue FromEntity(Entity value) => new() { Type = UnitValueType.Entity, Entity = value };
    public static UnitSourceValue FromString(in FixedString128Bytes value) => new() { Type = UnitValueType.String, String = value };
    public static UnitSourceValue FromString(string value)
    {
        FixedString128Bytes fixedString = default;
        return fixedString.CopyFrom(value ?? string.Empty) == CopyError.None
            ? FromString(in fixedString)
            : None;
    }

    public bool TryGetNumber(out float value)
    {
        if (Type == UnitValueType.Int)
        {
            value = Int;
            return true;
        }

        if (Type == UnitValueType.Float)
        {
            value = Float;
            return true;
        }

        value = 0f;
        return false;
    }

    public bool TryGetInt(out int value)
    {
        value = 0;
        if (!TryGetNumber(out float number) || !math.isfinite(number))
            return false;

        int rounded = (int)math.round(number);
        if (math.abs(number - rounded) > 0.0001f)
            return false;

        value = rounded;
        return true;
    }

    public bool TryGetBool(out bool value)
    {
        value = Bool != 0;
        return Type == UnitValueType.Bool;
    }

    public bool TryGetFloat2(out float2 value)
    {
        value = Type == UnitValueType.Float2 ? Float2 : float2.zero;
        return Type == UnitValueType.Float2;
    }

    public bool TryGetFloat3(out float3 value)
    {
        value = Type == UnitValueType.Float3 ? Float3 : float3.zero;
        return Type == UnitValueType.Float3;
    }

    public bool TryGetEntity(out Entity value)
    {
        value = Type == UnitValueType.Entity ? Entity : Unity.Entities.Entity.Null;
        return Type == UnitValueType.Entity;
    }

    public bool TryGetString(out FixedString128Bytes value)
    {
        value = Type == UnitValueType.String ? String : default;
        return Type == UnitValueType.String;
    }

    public static bool TryFromUnitValue(in UnitValue source, out UnitSourceValue value)
    {
        value = source.Type switch
        {
            UnitValueType.Bool => FromBool(source.Bool),
            UnitValueType.Int => FromInt(source.Int),
            UnitValueType.Float => FromFloat(source.Float),
            UnitValueType.Float2 => FromFloat2(source.Float2),
            UnitValueType.Float3 => FromFloat3(source.Float3),
            UnitValueType.Entity => FromEntity(source.Entity),
            UnitValueType.String => FromString(source.String),
            _ => None,
        };
        return value.Type != UnitValueType.None;
    }

    public UnitValue ToUnitValue()
    {
        return Type switch
        {
            UnitValueType.Bool => UnitValue.FromBool(Bool != 0),
            UnitValueType.Int => UnitValue.FromInt(Int),
            UnitValueType.Float => UnitValue.FromFloat(Float),
            UnitValueType.Float2 => UnitValue.FromFloat2(Float2),
            UnitValueType.Float3 => UnitValue.FromFloat3(Float3),
            UnitValueType.Entity => UnitValue.FromEntity(Entity),
            UnitValueType.String => UnitValue.FromString(String.ToString()),
            _ => UnitValue.None,
        };
    }
}

public struct UnitSourceArguments
{
    public FixedList4096Bytes<UnitSourceValue> Values;
    public FixedString128Bytes Key;
    public byte HasKey;

    public int Count => Values.Length;

    public bool TryGet(int index, out UnitSourceValue value)
    {
        if ((uint)index >= (uint)Values.Length)
        {
            value = default;
            return false;
        }

        value = Values[index];
        return true;
    }

    public bool TryGetNumber(int index, out float value)
    {
        value = 0f;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetNumber(out value);
    }

    public bool TryGetInt(int index, out int value)
    {
        value = 0;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetInt(out value);
    }

    public bool TryGetBool(int index, out bool value)
    {
        value = false;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetBool(out value);
    }

    public bool TryGetFloat2(int index, out float2 value)
    {
        value = float2.zero;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetFloat2(out value);
    }

    public bool TryGetFloat3(int index, out float3 value)
    {
        value = float3.zero;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetFloat3(out value);
    }

    public bool TryGetEntity(int index, out Entity value)
    {
        value = Entity.Null;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetEntity(out value);
    }

    public bool TryGetString(int index, out FixedString128Bytes value)
    {
        value = default;
        return TryGet(index, out UnitSourceValue sourceValue) && sourceValue.TryGetString(out value);
    }

    public static bool TryCreate(UnitValue[] values, string key, out UnitSourceArguments arguments)
    {
        arguments = default;
        if (!string.IsNullOrWhiteSpace(key))
        {
            if (arguments.Key.CopyFrom(key) != CopyError.None)
                return false;

            arguments.HasKey = 1;
        }

        UnitValue[] input = values ?? System.Array.Empty<UnitValue>();
        for (int index = 0; index < input.Length; index++)
        {
            if (!UnitSourceValue.TryFromUnitValue(input[index], out UnitSourceValue value) ||
                arguments.Values.Length >= arguments.Values.Capacity)
            {
                arguments = default;
                return false;
            }

            arguments.Values.Add(value);
        }

        return true;
    }
}
