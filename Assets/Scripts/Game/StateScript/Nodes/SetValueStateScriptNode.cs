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
    private CompiledValueExpression[] _valueExpressions;

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
        _valueExpressions = null;
        if (string.IsNullOrWhiteSpace(_data.SetterKey))
        {
            error = "SetValue setter key is empty.";
            return false;
        }

        if (!Runtime.Sources.TryGetDefinition(_data.SetterKey, _data.SourceTarget, out UnitSourceSet set))
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

        CompiledValueExpression[] valueExpressions = new CompiledValueExpression[set.Parameters.Count];
        for (int i = 0; i < set.Parameters.Count; i++)
        {
            ComparatorParameterDefinition parameter = set.Parameters[i];
            ValueExpression value = values[i] ?? new ValueExpression();
            values[i] = value;
            if (!s_expressionFactory.TryBuildValueExpression(
                    value,
                    Runtime.Sources,
                    out CompiledValueExpression valueExpression,
                    out error))
            {
                return false;
            }

            if (!parameter.Accepts(valueExpression.Category))
            {
                error = $"Setter '{_data.SetterKey}' input '{parameter.Name}' requires {parameter.Category}, but received {valueExpression.Category}.";
                return false;
            }

            valueExpressions[i] = valueExpression;
        }

        _set = set;
        _key = _data.Key ?? string.Empty;
        _valueExpressions = valueExpressions;
        error = string.Empty;
        return true;
    }

    private void Execute()
    {
        if (_set == null || _valueExpressions == null)
            return;

        UnitSourceArguments arguments = default;
        for (int i = 0; i < _valueExpressions.Length; i++)
        {
            if (arguments.Values.Length >= arguments.Values.Capacity ||
                !_valueExpressions[i].TryEvaluate(Runtime.Sources, out UnitSourceValue value))
                return;

            arguments.Values.Add(value);
        }

        UnitSourceValue keyedValue = arguments.Count > 0 ? arguments.Values[0] : default;
        bool didSet = _set.RequiresKey
            ? _set.TrySet(_key, in keyedValue)
            : _set.TrySet(in arguments);
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
