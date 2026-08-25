using System;

public sealed class Condition
{
    private readonly ICompareType _compareType;
    private readonly Func<UnitValue>[] _inputGetters;
    private readonly UnitValue[] _values;

    public Condition(ConditionType type, ICompareType compareType, Func<UnitValue>[] inputGetters)
    {
        Type = type;
        _compareType = compareType;
        _inputGetters = inputGetters;
        _values = new UnitValue[inputGetters?.Length ?? 0];
    }

    public ConditionType Type { get; }

    public bool Compare()
    {
        if (_compareType == null || _inputGetters == null)
            return false;

        for (int i = 0; i < _inputGetters.Length; i++)
        {
            Func<UnitValue> getter = _inputGetters[i];
            if (getter == null)
                return false;

            _values[i] = getter();
        }

        return _compareType.TryCompare(_values, out bool result) && result;
    }
}

public enum ConditionType
{
    Necessary,
    Unallowed,
}
