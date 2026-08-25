using CrystalMagic.Game.Data;

[FactoryKey("Compare", 0, "Compare")]
public sealed class CompareStateScriptNode : StateScriptBoolNode
{
    private static readonly ComparatorFactory s_comparatorFactory = CreateComparatorFactory();

    private readonly CompareStateScriptNodeData _data;
    private readonly StateScriptOutputPort _trueOutput;
    private readonly StateScriptOutputPort _falseOutput;
    private Comparator _comparator;

    public CompareStateScriptNode(CompareStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("Check", Check);
        _trueOutput = AddOutput("True");
        _falseOutput = AddOutput("False");
    }

    protected override bool OnBind(out string error)
    {
        _data.Condition ??= new ConditionConfig();
        _data.Condition.ConditionType = ConditionType.Necessary;
        _comparator = s_comparatorFactory.BuildComparator(new[] { _data.Condition }, Runtime.Sources);
        if (!_comparator.IsValid)
        {
            error = "Compare comparator is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void Check()
    {
        if (_comparator == null)
            return;

        if (_comparator.GetResult())
            _trueOutput.Pulse();
        else
            _falseOutput.Pulse();
    }

    private static ComparatorFactory CreateComparatorFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
