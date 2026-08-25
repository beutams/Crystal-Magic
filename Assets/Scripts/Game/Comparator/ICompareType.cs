using System.Collections.Generic;

public interface ICompareType
{
    IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    bool TryCompare(UnitValue[] values, out bool result);
}

[FactoryKey("Equal")]
[EditorLabel("Equal")]
public sealed class Equal : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Left", UnitValueCategory.Any),
        new("Right", UnitValueCategory.Any),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (!HasCount(values, 2))
        {
            result = false;
            return false;
        }

        result = values[0].EqualsValue(values[1]);
        return true;
    }

    private static bool HasCount(UnitValue[] values, int count) => values != null && values.Length == count;
}

[FactoryKey("NotEqual")]
[EditorLabel("Not equal")]
public sealed class NotEqual : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Left", UnitValueCategory.Any),
        new("Right", UnitValueCategory.Any),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (values == null || values.Length != 2)
        {
            result = false;
            return false;
        }

        result = !values[0].EqualsValue(values[1]);
        return true;
    }
}

[FactoryKey("GreaterThan")]
[EditorLabel("Greater than")]
public sealed class GreaterThan : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Left", UnitValueCategory.Number),
        new("Right", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (!TryGetNumbers(values, out float left, out float right))
        {
            result = false;
            return false;
        }

        result = left > right;
        return true;
    }

    internal static bool TryGetNumbers(UnitValue[] values, out float left, out float right)
    {
        if (values != null && values.Length == 2 &&
            values[0].TryGetNumber(out left) && values[1].TryGetNumber(out right))
            return true;

        left = 0f;
        right = 0f;
        return false;
    }
}

[FactoryKey("GreaterOrEqual")]
[EditorLabel("Greater or equal")]
public sealed class GreaterOrEqual : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Left", UnitValueCategory.Number),
        new("Right", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (!GreaterThan.TryGetNumbers(values, out float left, out float right))
        {
            result = false;
            return false;
        }

        result = left >= right;
        return true;
    }
}

[FactoryKey("LessThan")]
[EditorLabel("Less than")]
public sealed class LessThan : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Left", UnitValueCategory.Number),
        new("Right", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (!GreaterThan.TryGetNumbers(values, out float left, out float right))
        {
            result = false;
            return false;
        }

        result = left < right;
        return true;
    }
}

[FactoryKey("LessOrEqual")]
[EditorLabel("Less or equal")]
public sealed class LessOrEqual : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Left", UnitValueCategory.Number),
        new("Right", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (!GreaterThan.TryGetNumbers(values, out float left, out float right))
        {
            result = false;
            return false;
        }

        result = left <= right;
        return true;
    }
}

[FactoryKey("IsTrue")]
[EditorLabel("Is true")]
public sealed class IsTrue : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Value", UnitValueCategory.Any),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (values == null || values.Length != 1)
        {
            result = false;
            return false;
        }

        if (values[0].Type == UnitValueType.Bool)
        {
            result = values[0].Bool;
            return true;
        }

        if (values[0].TryGetNumber(out float number))
        {
            result = number > 0f;
            return true;
        }

        result = false;
        return false;
    }
}

[FactoryKey("IsFalse")]
[EditorLabel("Is false")]
public sealed class IsFalse : ICompareType
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Value", UnitValueCategory.Any),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

    public bool TryCompare(UnitValue[] values, out bool result)
    {
        if (values == null || values.Length != 1)
        {
            result = false;
            return false;
        }

        if (values[0].Type == UnitValueType.Bool)
        {
            result = !values[0].Bool;
            return true;
        }

        if (values[0].TryGetNumber(out float number))
        {
            result = number <= 0f;
            return true;
        }

        result = false;
        return false;
    }
}
