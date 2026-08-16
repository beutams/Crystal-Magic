using CrystalMagic.Game.Data;
using UnityEngine;

[FactoryKey("NumberMonitor", 23, "Number Monitor")]
public sealed class NumberMonitorStateScriptNode : StateScriptStateNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly NumberMonitorStateScriptNodeData _data;
    private readonly StateScriptOutputPort _onValueChangeOutput;
    private System.Func<UnitValue> _valueGetter;
    private bool _hasLastValue;
    private float _lastValue;

    public NumberMonitorStateScriptNode(NumberMonitorStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        _onValueChangeOutput = AddOutput("OnValueChange");
    }

    protected override bool OnBind(out string error)
    {
        _valueGetter = null;
        _data.Value ??= NumberMonitorStateScriptNodeData.CreateDefaultValueExpression();
        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Value,
                Runtime.Sources,
                out UnitValueCategory category,
                out System.Func<UnitValue> valueGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Number)
        {
            error = $"Number Monitor Value requires Number, but received {category}.";
            return false;
        }

        _valueGetter = valueGetter;
        error = string.Empty;
        return true;
    }

    protected override void OnActivate()
    {
        _hasLastValue = false;
    }

    protected override void OnUpdate()
    {
        if (!TryGetValue(out float value))
            return;

        if (_hasLastValue && !Mathf.Approximately(value, _lastValue))
        {
            _onValueChangeOutput.Pulse();
            if (Status == StateScriptStateStatus.Stop)
                return;
        }

        _lastValue = value;
        _hasLastValue = true;
    }

    private bool TryGetValue(out float value)
    {
        value = 0f;
        if (_valueGetter == null || !_valueGetter().TryGetNumber(out value))
            return false;

        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
