using CrystalMagic.Game.Data;
using UnityEngine;

[FactoryKey("Timer", 20, "Timer")]
public sealed class TimerStateScriptNode : StateScriptStateNode
{
    private readonly TimerStateScriptNodeData _data;
    private float _elapsedSeconds;

    public TimerStateScriptNode(TimerStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
    }

    protected override void OnActivate()
    {
        _elapsedSeconds = 0f;
    }

    protected override void OnUpdate()
    {
        _elapsedSeconds += Runtime.DeltaTime;
        if (_elapsedSeconds >= Mathf.Max(0f, _data.DurationSeconds))
            Complete();
    }
}
