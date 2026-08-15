using CrystalMagic.Game.Data;

[FactoryKey("Entry", -100, "Entry")]
public sealed class StateScriptEntryNode : StateScriptNode
{
    private readonly StateScriptOutputPort _output;

    public StateScriptEntryNode(StateScriptEntryNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _output = AddOutput("Out");
    }

    public void Start()
    {
        RecordPulse();
        _output.Pulse();
    }
}
