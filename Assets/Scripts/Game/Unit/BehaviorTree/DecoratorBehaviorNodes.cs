using CrystalMagic.Game.Data;
using UnityEngine;

internal static class DecoratorLoopGuard
{
    // Prevent accidental infinite tight loops when a decorator is configured
    // as infinite and its child resolves synchronously every tick.
    public const int MaxImmediateIterationsPerTick = 256;
}

[FactoryKey(BehaviorNodeTypes.Inverter, 20, "Inverter")]
public sealed class InverterBehaviorNode : DecoratorBehaviorNode
{
    public InverterBehaviorNode(InverterBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        BehaviorNodeStatus status = Child.Tick(context);
        return status switch
        {
            BehaviorNodeStatus.Success => BehaviorNodeStatus.Failure,
            BehaviorNodeStatus.Failure => BehaviorNodeStatus.Success,
            _ => status,
        };
    }
}

[FactoryKey(BehaviorNodeTypes.Succeeder, 21, "Succeeder")]
public sealed class SucceederBehaviorNode : DecoratorBehaviorNode
{
    public SucceederBehaviorNode(SucceederBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Success;

        BehaviorNodeStatus status = Child.Tick(context);
        return status == BehaviorNodeStatus.Running
            ? BehaviorNodeStatus.Running
            : BehaviorNodeStatus.Success;
    }
}

[FactoryKey(BehaviorNodeTypes.Failer, 22, "Failer")]
public sealed class FailerBehaviorNode : DecoratorBehaviorNode
{
    public FailerBehaviorNode(FailerBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        BehaviorNodeStatus status = Child.Tick(context);
        return status == BehaviorNodeStatus.Running
            ? BehaviorNodeStatus.Running
            : BehaviorNodeStatus.Failure;
    }
}

[FactoryKey(BehaviorNodeTypes.Repeater, 23, "Repeater")]
public sealed class RepeaterBehaviorNode : DecoratorBehaviorNode
{
    private readonly RepeaterBehaviorNodeData _data;
    private int _completedCount;

    public RepeaterBehaviorNode(RepeaterBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        if (_data.RepeatCount == 0)
            return BehaviorNodeStatus.Success;

        int maxIterations = _data.RepeatCount < 0
            ? DecoratorLoopGuard.MaxImmediateIterationsPerTick
            : Mathf.Max(0, _data.RepeatCount - _completedCount);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            BehaviorNodeStatus status = Child.Tick(context);
            switch (status)
            {
                case BehaviorNodeStatus.Running:
                    return BehaviorNodeStatus.Running;

                case BehaviorNodeStatus.Failure:
                    Child.Reset();
                    _completedCount = 0;
                    return BehaviorNodeStatus.Failure;

                case BehaviorNodeStatus.Success:
                    _completedCount++;
                    Child.Reset();

                    if (_data.RepeatCount >= 0 && _completedCount >= _data.RepeatCount)
                    {
                        _completedCount = 0;
                        return BehaviorNodeStatus.Success;
                    }

                    break;

                default:
                    Child.Reset();
                    _completedCount = 0;
                    return BehaviorNodeStatus.Failure;
            }
        }

        if (_data.RepeatCount < 0)
        {
            Child.Reset();
            return BehaviorNodeStatus.Running;
        }

        if (_completedCount >= _data.RepeatCount)
        {
            _completedCount = 0;
            return BehaviorNodeStatus.Success;
        }

        return BehaviorNodeStatus.Running;
    }

    public override void Reset()
    {
        _completedCount = 0;
        base.Reset();
    }
}

[FactoryKey(BehaviorNodeTypes.UntilSuccess, 24, "Until Success")]
public sealed class UntilSuccessBehaviorNode : DecoratorBehaviorNode
{
    public UntilSuccessBehaviorNode(UntilSuccessBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        for (int iteration = 0; iteration < DecoratorLoopGuard.MaxImmediateIterationsPerTick; iteration++)
        {
            BehaviorNodeStatus status = Child.Tick(context);
            if (status == BehaviorNodeStatus.Success)
                return BehaviorNodeStatus.Success;

            if (status == BehaviorNodeStatus.Running)
                return BehaviorNodeStatus.Running;

            Child.Reset();
        }

        return BehaviorNodeStatus.Running;
    }
}

[FactoryKey(BehaviorNodeTypes.UntilFailure, 25, "Until Failure")]
public sealed class UntilFailureBehaviorNode : DecoratorBehaviorNode
{
    public UntilFailureBehaviorNode(UntilFailureBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        for (int iteration = 0; iteration < DecoratorLoopGuard.MaxImmediateIterationsPerTick; iteration++)
        {
            BehaviorNodeStatus status = Child.Tick(context);
            if (status == BehaviorNodeStatus.Failure)
                return BehaviorNodeStatus.Success;

            if (status == BehaviorNodeStatus.Running)
                return BehaviorNodeStatus.Running;

            Child.Reset();
        }

        return BehaviorNodeStatus.Running;
    }

    public override void Reset()
    {
        base.Reset();
    }
}

[FactoryKey(BehaviorNodeTypes.Cooldown, 26, "Cooldown")]
public sealed class CooldownBehaviorNode : DecoratorBehaviorNode
{
    private readonly CooldownBehaviorNodeData _data;
    private float _cooldownRemaining;

    public CooldownBehaviorNode(CooldownBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Mathf.Max(0f, context?.DeltaTime ?? 0f));
            if (_cooldownRemaining > 0f)
                return BehaviorNodeStatus.Failure;
        }

        BehaviorNodeStatus status = Child.Tick(context);
        if (status != BehaviorNodeStatus.Running)
            _cooldownRemaining = Mathf.Max(0f, _data.CooldownSeconds);

        return status;
    }

    public override void Reset()
    {
        _cooldownRemaining = 0f;
        base.Reset();
    }
}

[FactoryKey(BehaviorNodeTypes.Timeout, 27, "Timeout")]
public sealed class TimeoutBehaviorNode : DecoratorBehaviorNode
{
    private readonly TimeoutBehaviorNodeData _data;
    private float _elapsed;
    private bool _timing;

    public TimeoutBehaviorNode(TimeoutBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Child == null)
            return BehaviorNodeStatus.Failure;

        if (!_timing)
            _elapsed = 0f;

        BehaviorNodeStatus status = Child.Tick(context);
        if (status == BehaviorNodeStatus.Running)
        {
            _timing = true;
            _elapsed += Mathf.Max(0f, context?.DeltaTime ?? 0f);
            if (_data.TimeoutSeconds > 0f && _elapsed >= _data.TimeoutSeconds)
            {
                Child.Reset();
                _elapsed = 0f;
                _timing = false;
                return BehaviorNodeStatus.Failure;
            }

            return BehaviorNodeStatus.Running;
        }

        _elapsed = 0f;
        _timing = false;
        return status;
    }

    public override void Reset()
    {
        _elapsed = 0f;
        _timing = false;
        base.Reset();
    }
}
