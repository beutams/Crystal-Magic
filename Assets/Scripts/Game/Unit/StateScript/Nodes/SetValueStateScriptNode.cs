using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

[FactoryKey("SetValue", 10, "Set Value")]
public sealed class SetValueStateScriptNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly SetValueStateScriptNodeData _data;
    private readonly StateScriptOutputPort _output;
    private UnitSourceSet _set;
    private string _key;
    private Func<UnitValue>[] _valueGetters;

    public SetValueStateScriptNode(SetValueStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", Execute);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _set = null;
        _key = string.Empty;
        _valueGetters = null;
        if (string.IsNullOrWhiteSpace(_data.SetterKey))
        {
            error = "SetValue setter key is empty.";
            return false;
        }

        if (!Runtime.Sources.TryGetDefinition(_data.SetterKey, out UnitSourceSet set))
        {
            error = $"SetValue requires Source Set '{_data.SetterKey}'.";
            return false;
        }

        if (set.RequiresKey && string.IsNullOrWhiteSpace(_data.Key))
        {
            error = $"Setter '{_data.SetterKey}' requires a configured key.";
            return false;
        }

        List<ValueExpression> values = _data.GetOrCreateValues(set.Parameters.Count);
        if (values.Count != set.Parameters.Count)
        {
            error = $"Setter '{_data.SetterKey}' requires {set.Parameters.Count} inputs, but has {values.Count}.";
            return false;
        }

        Func<UnitValue>[] valueGetters = new Func<UnitValue>[set.Parameters.Count];
        for (int i = 0; i < set.Parameters.Count; i++)
        {
            ComparatorParameterDefinition parameter = set.Parameters[i];
            ValueExpression value = values[i] ?? new ValueExpression();
            values[i] = value;
            if (!s_expressionFactory.TryBuildValueExpression(
                    value,
                    Runtime.Sources,
                    out UnitValueCategory category,
                    out Func<UnitValue> valueGetter,
                    out error))
            {
                return false;
            }

            if (!parameter.Accepts(category))
            {
                error = $"Setter '{_data.SetterKey}' input '{parameter.Name}' requires {parameter.Category}, but received {category}.";
                return false;
            }

            valueGetters[i] = valueGetter;
        }

        _set = set;
        _key = _data.Key ?? string.Empty;
        _valueGetters = valueGetters;
        error = string.Empty;
        return true;
    }

    private void Execute()
    {
        if (_set == null || _valueGetters == null)
            return;

        UnitValue[] values = new UnitValue[_valueGetters.Length];
        for (int i = 0; i < _valueGetters.Length; i++)
            values[i] = _valueGetters[i]();

        bool didSet = _set.RequiresKey
            ? _set.TrySet(_key, values[0])
            : _set.TrySet(values);
        if (didSet)
            _output.Pulse();
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
