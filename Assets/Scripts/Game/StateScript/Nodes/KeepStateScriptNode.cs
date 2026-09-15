using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

[FactoryKey("Keep", 21, "Keep")]
public sealed class KeepStateScriptNode : StateScriptStateNode
{
    private readonly KeepStateScriptNodeData _data;
    private readonly StateScriptOutputPort _onTimeStartOutput;
    private readonly StateScriptOutputPort _onTimeTickOutput;
    private readonly StateScriptOutputPort _onTimeCompleteOutput;
    private readonly StateScriptOutputPort _onTimeStopOutput;
    private float _elapsedSeconds;
    private long _timingStartTick;
    private long _lastKeepTick;
    private bool _isTiming;

    public KeepStateScriptNode(KeepStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("Keep", Keep);
        _onTimeStartOutput = AddOutput("OnTimeStart");
        _onTimeTickOutput = AddOutput("OnTimeTick");
        _onTimeCompleteOutput = AddOutput("OnTimeComplete");
        _onTimeStopOutput = AddOutput("OnTimeStop");
    }

    protected override void OnActivate()
    {
        _elapsedSeconds = 0f;
        _timingStartTick = long.MinValue;
        _lastKeepTick = long.MinValue;
        _isTiming = false;
    }

    protected override void OnUpdate()
    {
        if (!_isTiming)
            return;

        long requiredKeepTick = System.Math.Max(_timingStartTick, Runtime.TickVersion - 1);
        if (_lastKeepTick < requiredKeepTick)
        {
            _isTiming = false;
            _onTimeStopOutput.Pulse();
            if (Status == StateScriptStateStatus.Stop)
                return;

            Stop();
            return;
        }

        _elapsedSeconds += Runtime.DeltaTime;
        _onTimeTickOutput.Pulse();
        if (Status == StateScriptStateStatus.Stop)
            return;

        if (_elapsedSeconds >= Mathf.Max(0f, _data.DurationSeconds))
        {
            _isTiming = false;
            _onTimeCompleteOutput.Pulse();
            if (Status == StateScriptStateStatus.Stop)
                return;

            Complete();
        }
    }

    private void Keep()
    {
        if (Status == StateScriptStateStatus.Stop)
            return;

        long tickVersion = Runtime.TickVersion;
        _lastKeepTick = tickVersion;
        if (_isTiming)
            return;

        _isTiming = true;
        _timingStartTick = tickVersion;
        _onTimeStartOutput.Pulse();
    }

    public override void CollectRuntimeDebugData(List<StateScriptRuntimeDebugValue> values)
    {
        base.CollectRuntimeDebugData(values);
        values.Add(new StateScriptRuntimeDebugValue("Timing", _isTiming.ToString()));
        values.Add(new StateScriptRuntimeDebugValue("Elapsed", $"{_elapsedSeconds:0.###} s"));
        values.Add(new StateScriptRuntimeDebugValue("Duration", $"{Mathf.Max(0f, _data.DurationSeconds):0.###} s"));
        values.Add(new StateScriptRuntimeDebugValue("Timing Start Tick", _timingStartTick.ToString()));
        values.Add(new StateScriptRuntimeDebugValue("Last Keep Tick", _lastKeepTick.ToString()));
    }
}
