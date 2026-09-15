using CrystalMagic.Game.Data;

public abstract class StateScriptActionNode : StateScriptNode
{
    protected StateScriptActionNode(ActionStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
    }
}
