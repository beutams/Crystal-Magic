using System;
using System.Collections.Generic;

public abstract class StateScriptPort
{
    protected StateScriptPort(StateScriptNode owner, string name)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Name = name ?? string.Empty;
    }

    public StateScriptNode Owner { get; }
    public string Name { get; }
}

public sealed class StateScriptInputPort : StateScriptPort
{
    private readonly Action _callback;

    internal StateScriptInputPort(StateScriptNode owner, string name, Action callback)
        : base(owner, name)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public void Pulse()
    {
        Owner.RecordPulse();
        _callback();
    }
}

public sealed class StateScriptOutputPort : StateScriptPort
{
    private readonly List<StateScriptInputPort> _targets = new();

    internal StateScriptOutputPort(StateScriptNode owner, string name)
        : base(owner, name)
    {
    }

    public IReadOnlyList<StateScriptInputPort> Targets => _targets;

    public void Connect(StateScriptInputPort target)
    {
        if (target != null && !_targets.Contains(target))
            _targets.Add(target);
    }

    public void Disconnect(StateScriptInputPort target)
    {
        _targets.Remove(target);
    }

    public void Pulse()
    {
        StateScriptRuntime runtime = Owner.Runtime;
        if (runtime != null && !runtime.TryEnterPulse())
            return;

        try
        {
            for (int i = 0; i < _targets.Count; i++)
                _targets[i].Pulse();
        }
        finally
        {
            runtime?.ExitPulse();
        }
    }
}
