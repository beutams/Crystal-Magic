using CrystalMagic.Game.Data;
using UnityEngine;

[FactoryKey("Keep", 21, "Keep")]
public sealed class KeepStateScriptNode : StateScriptStateNode
{
    private readonly KeepStateScriptNodeData _data;
    private float _elapsedSeconds;
    private long _activationTick;
    private long _lastStartPulseTick = long.MinValue;

    public KeepStateScriptNode(KeepStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
    }

    protected override void OnActivate()
    {
        _elapsedSeconds = 0f;
        _activationTick = Runtime.TickVersion;
    }

    protected override void OnUpdate()
    {
        long requiredKeepTick = System.Math.Max(_activationTick, Runtime.TickVersion - 1);
        if (_lastStartPulseTick < requiredKeepTick)
        {
            Stop();
            return;
        }

        _elapsedSeconds += Runtime.DeltaTime;
        if (_elapsedSeconds >= Mathf.Max(0f, _data.DurationSeconds))
            Complete();
    }

    protected override void OnStartPulse()
    {
        _lastStartPulseTick = Runtime?.TickVersion ?? long.MinValue;
    }
}
