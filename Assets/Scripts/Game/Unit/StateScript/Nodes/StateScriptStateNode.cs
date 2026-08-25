using CrystalMagic.Game.Data;

public enum StateScriptStateStatus
{
    Stop,
    Pending,
    Running,
}

public abstract class StateScriptStateNode : StateScriptNode
{
    private readonly StateScriptOutputPort _onStartOutput;
    private readonly StateScriptOutputPort _onTickOutput;
    private readonly StateScriptOutputPort _onCompleteOutput;
    private readonly StateScriptOutputPort _onAbortOutput;
    private readonly StateScriptOutputPort _onStopOutput;
    private bool _hasAbortInput;
    private long _pendingTick = -1;

    protected StateScriptStateNode(
        StateStateScriptNodeData data,
        StateScriptRuntime runtime,
        bool addAbortInput = true)
        : base(data, runtime)
    {
        AddInput("Start", Start);
        if (addAbortInput)
            AddAbortInput();
        _onStartOutput = AddOutput("OnStart");
        _onTickOutput = AddOutput("OnTick");
        _onCompleteOutput = AddOutput("OnComplete");
        _onAbortOutput = AddOutput("OnAbort");
        _onStopOutput = AddOutput("OnStop");
    }

    public StateScriptStateStatus Status { get; private set; } = StateScriptStateStatus.Stop;

    internal void TryEnterRunning(long tickVersion)
    {
        if (Status == StateScriptStateStatus.Pending && _pendingTick < tickVersion)
            Status = StateScriptStateStatus.Running;
    }

    internal void TryUpdate()
    {
        if (Status != StateScriptStateStatus.Running)
            return;

        OnUpdate();
        if (Status == StateScriptStateStatus.Running)
            _onTickOutput.Pulse();
    }

    internal void StopWithoutOutput()
    {
        if (Status == StateScriptStateStatus.Stop)
            return;

        Status = StateScriptStateStatus.Stop;
        OnDeactivate();
    }

    protected void Complete()
    {
        if (Status == StateScriptStateStatus.Stop)
            return;

        Status = StateScriptStateStatus.Stop;
        OnComplete();
        OnDeactivate();
        _onCompleteOutput.Pulse();
    }

    protected void Abort()
    {
        if (Status == StateScriptStateStatus.Stop)
            return;

        Status = StateScriptStateStatus.Stop;
        OnAbort();
        OnDeactivate();
        _onAbortOutput.Pulse();
    }

    protected void Stop()
    {
        if (Status == StateScriptStateStatus.Stop)
            return;

        Status = StateScriptStateStatus.Stop;
        OnStop();
        OnDeactivate();
        _onStopOutput.Pulse();
    }

    protected void AddAbortInput()
    {
        if (_hasAbortInput)
            return;

        AddInput("Abort", Abort);
        _hasAbortInput = true;
    }

    protected virtual void OnActivate()
    {
    }

    protected virtual void OnUpdate()
    {
    }

    protected virtual void OnComplete()
    {
    }

    protected virtual void OnStop()
    {
    }

    protected virtual void OnAbort()
    {
    }

    protected virtual void OnDeactivate()
    {
    }

    private void Start()
    {
        if (Status != StateScriptStateStatus.Stop)
            return;

        Status = StateScriptStateStatus.Pending;
        _pendingTick = Runtime?.TickVersion ?? 0;
        OnActivate();
        if (Status != StateScriptStateStatus.Stop)
            _onStartOutput.Pulse();
    }

}
