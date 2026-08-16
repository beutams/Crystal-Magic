using System;
using CrystalMagic.Game.Data;

[FactoryKey("SetValue", 10, "Set Value")]
public sealed class SetValueStateScriptNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly SetValueStateScriptNodeData _data;
    private readonly StateScriptOutputPort _output;
    private UnitSourceSet _set;
    private string _key;
    private Func<UnitValue> _valueGetter;

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
        _valueGetter = null;
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

        if (set.Parameters.Count != 1)
        {
            error = $"Setter '{_data.SetterKey}' must define exactly one input.";
            return false;
        }

        if (set.RequiresKey && string.IsNullOrWhiteSpace(_data.Key))
        {
            error = $"Setter '{_data.SetterKey}' requires a configured key.";
            return false;
        }

        _data.Value ??= new ValueExpression();
        ComparatorParameterDefinition parameter = set.Parameters[0];
        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Value,
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

        _set = set;
        _key = _data.Key ?? string.Empty;
        _valueGetter = valueGetter;
        error = string.Empty;
        return true;
    }

    private void Execute()
    {
        if (_set == null || _valueGetter == null)
            return;

        UnitValue value = _valueGetter();
        bool didSet = _set.RequiresKey
            ? _set.TrySet(_key, value)
            : _set.TrySet(new[] { value });
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
