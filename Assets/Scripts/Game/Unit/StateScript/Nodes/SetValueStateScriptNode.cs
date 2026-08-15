using CrystalMagic.Game.Data;

[FactoryKey("SetValue", 10, "Set Value")]
public sealed class SetValueStateScriptNode : StateScriptActionNode
{
    private readonly SetValueStateScriptNodeData _data;
    private readonly StateScriptOutputPort _output;
    private UnitSourceSet _set;
    private UnitValue[] _inputs;

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
        _inputs = null;
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

        _data.Arguments ??= new System.Collections.Generic.List<UnitValue>();
        if (_data.Arguments.Count != set.Parameters.Count)
        {
            error = $"Setter '{_data.SetterKey}' requires {set.Parameters.Count} argument(s), but received {_data.Arguments.Count}.";
            return false;
        }

        for (int i = 0; i < set.Parameters.Count; i++)
        {
            if (!set.Parameters[i].Accepts(_data.Arguments[i].Category))
            {
                error = $"Setter '{_data.SetterKey}' argument '{set.Parameters[i].Name}' requires {set.Parameters[i].Category}, but received {_data.Arguments[i].Category}.";
                return false;
            }
        }

        _set = set;
        _inputs = _data.Arguments.ToArray();
        error = string.Empty;
        return true;
    }

    private void Execute()
    {
        if (_set == null)
            return;

        if (_set.TrySet(_inputs))
            _output.Pulse();
    }
}
