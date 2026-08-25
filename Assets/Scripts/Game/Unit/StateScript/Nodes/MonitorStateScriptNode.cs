using CrystalMagic.Game.Data;

[FactoryKey("Monitor", 22, "Monitor")]
public sealed class MonitorStateScriptNode : StateScriptStateNode
{
    private static readonly ComparatorFactory s_comparatorFactory = CreateComparatorFactory();

    private readonly MonitorStateScriptNodeData _data;
    private readonly StateScriptOutputPort _trueOutput;
    private readonly StateScriptOutputPort _falseOutput;
    private readonly StateScriptOutputPort _onChangeTrueOutput;
    private readonly StateScriptOutputPort _onChangeFalseOutput;
    private Comparator _comparator;
    private bool _hasLastValue;
    private bool _lastValue;

    public MonitorStateScriptNode(MonitorStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        _trueOutput = AddOutput("True");
        _falseOutput = AddOutput("False");
        _onChangeTrueOutput = AddOutput("OnChangeTrue");
        _onChangeFalseOutput = AddOutput("OnChangeFalse");
    }

    protected override bool OnBind(out string error)
    {
        _data.Condition ??= new ConditionConfig();
        _data.Condition.ConditionType = ConditionType.Necessary;
        _comparator = s_comparatorFactory.BuildComparator(new[] { _data.Condition }, Runtime.Sources);
        if (!_comparator.IsValid)
        {
            error = "Monitor comparator is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected override void OnActivate()
    {
        _lastValue = _comparator.GetResult();
        _hasLastValue = true;
    }

    protected override void OnUpdate()
    {
        bool value = _comparator.GetResult();

        if (_hasLastValue && value != _lastValue)
        {
            if (value)
                _onChangeTrueOutput.Pulse();
            else
                _onChangeFalseOutput.Pulse();

            if (Status == StateScriptStateStatus.Stop)
                return;
        }

        if (value)
            _trueOutput.Pulse();
        else
            _falseOutput.Pulse();

        _lastValue = value;
        _hasLastValue = true;
    }

    private static ComparatorFactory CreateComparatorFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
