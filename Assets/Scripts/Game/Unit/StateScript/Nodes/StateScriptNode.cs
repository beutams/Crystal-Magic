using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

public abstract class StateScriptNode
{
    private readonly List<StateScriptInputPort> _inputs = new();
    private readonly List<StateScriptOutputPort> _outputs = new();

    protected StateScriptNode(StateScriptNodeData data, StateScriptRuntime runtime)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Runtime = runtime;
    }

    public StateScriptNodeData Data { get; }
    public StateScriptRuntime Runtime { get; }
    public IReadOnlyList<StateScriptInputPort> Inputs => _inputs;
    public IReadOnlyList<StateScriptOutputPort> Outputs => _outputs;
    public long LastPulseTick { get; private set; } = -1;

    public bool TryGetInput(string name, out StateScriptInputPort port)
    {
        for (int i = 0; i < _inputs.Count; i++)
        {
            if (string.Equals(_inputs[i].Name, name, StringComparison.Ordinal))
            {
                port = _inputs[i];
                return true;
            }
        }

        port = null;
        return false;
    }

    public bool TryGetOutput(string name, out StateScriptOutputPort port)
    {
        for (int i = 0; i < _outputs.Count; i++)
        {
            if (string.Equals(_outputs[i].Name, name, StringComparison.Ordinal))
            {
                port = _outputs[i];
                return true;
            }
        }

        port = null;
        return false;
    }

    internal void RecordPulse()
    {
        LastPulseTick = Runtime?.TickVersion ?? -1;
    }

    internal bool TryBind(out string error)
    {
        if (Runtime == null)
        {
            error = string.Empty;
            return true;
        }

        return OnBind(out error);
    }

    protected StateScriptInputPort AddInput(string name, Action callback)
    {
        var port = new StateScriptInputPort(this, name, callback);
        _inputs.Add(port);
        return port;
    }

    protected StateScriptOutputPort AddOutput(string name)
    {
        var port = new StateScriptOutputPort(this, name);
        _outputs.Add(port);
        return port;
    }

    protected virtual bool OnBind(out string error)
    {
        error = string.Empty;
        return true;
    }
}
