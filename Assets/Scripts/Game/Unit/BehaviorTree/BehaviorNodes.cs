using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

public enum BehaviorNodeStatus
{
    Success,
    Failure,
    Running,
}

public abstract class ABehaviorNode
{
    protected readonly BehaviorNodeData Data;
    protected readonly List<ABehaviorNode> Children = new();

    protected ABehaviorNode(BehaviorNodeData data)
    {
        Data = data;
    }

    public string Guid => Data?.Guid ?? string.Empty;
    public string Type => Data?.Type ?? string.Empty;
    public string DisplayName => BehaviorNodeDataRegistry.GetDisplayName(Type);

    public virtual void AddChild(ABehaviorNode child)
    {
        if (child != null)
            Children.Add(child);
    }

    public BehaviorNodeStatus Tick(BehaviorTreeContext context)
    {
        context?.MarkCurrentNode(this);
        return OnTick(context);
    }

    public virtual void Reset()
    {
        for (int i = 0; i < Children.Count; i++)
            Children[i]?.Reset();
    }

    protected abstract BehaviorNodeStatus OnTick(BehaviorTreeContext context);
}

public abstract class CompositeBehaviorNode : ABehaviorNode
{
    protected CompositeBehaviorNode(BehaviorNodeData data)
        : base(data)
    {
    }
}

public abstract class ConditionBehaviorNode : ABehaviorNode
{
    protected ConditionBehaviorNode(BehaviorNodeData data)
        : base(data)
    {
    }
}

public abstract class ActionBehaviorNode : ABehaviorNode
{
    protected ActionBehaviorNode(BehaviorNodeData data)
        : base(data)
    {
    }
}

[FactoryKey(BehaviorNodeTypes.Root, -100, "Root")]
public sealed class RootBehaviorNode : ABehaviorNode
{
    public RootBehaviorNode(RootBehaviorNodeData data)
        : base(data)
    {
    }

    public override void AddChild(ABehaviorNode child)
    {
        if (child == null)
            return;

        Children.Clear();
        Children.Add(child);
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (Children.Count == 0 || Children[0] == null)
            return BehaviorNodeStatus.Failure;

        return Children[0].Tick(context);
    }
}

[FactoryKey(BehaviorNodeTypes.Selector, 0, "Selector")]
public sealed class SelectorBehaviorNode : CompositeBehaviorNode
{
    private int _runningChildIndex = -1;

    public SelectorBehaviorNode(SelectorBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        int startIndex = _runningChildIndex >= 0 ? _runningChildIndex : 0;
        for (int i = startIndex; i < Children.Count; i++)
        {
            BehaviorNodeStatus status = Children[i].Tick(context);
            if (status == BehaviorNodeStatus.Failure)
                continue;

            _runningChildIndex = status == BehaviorNodeStatus.Running ? i : -1;
            return status;
        }

        _runningChildIndex = -1;
        return BehaviorNodeStatus.Failure;
    }

    public override void Reset()
    {
        _runningChildIndex = -1;
        base.Reset();
    }
}

[FactoryKey(BehaviorNodeTypes.Sequence, 1, "Sequence")]
public sealed class SequenceBehaviorNode : CompositeBehaviorNode
{
    private int _runningChildIndex = -1;

    public SequenceBehaviorNode(SequenceBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        int startIndex = _runningChildIndex >= 0 ? _runningChildIndex : 0;
        for (int i = startIndex; i < Children.Count; i++)
        {
            BehaviorNodeStatus status = Children[i].Tick(context);
            if (status == BehaviorNodeStatus.Success)
                continue;

            _runningChildIndex = status == BehaviorNodeStatus.Running ? i : -1;
            return status;
        }

        _runningChildIndex = -1;
        return BehaviorNodeStatus.Success;
    }

    public override void Reset()
    {
        _runningChildIndex = -1;
        base.Reset();
    }
}

[FactoryKey(BehaviorNodeTypes.HasTarget, 10, "Has Target")]
public sealed class HasTargetBehaviorNode : ConditionBehaviorNode
{
    public HasTargetBehaviorNode(HasTargetBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        return context != null && context.Perception.HasTarget
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Failure;
    }
}

[FactoryKey(BehaviorNodeTypes.AcquireNearestEnemy, 11, "Acquire Nearest Enemy")]
public sealed class AcquireNearestEnemyBehaviorNode : ActionBehaviorNode
{
    public AcquireNearestEnemyBehaviorNode(AcquireNearestEnemyBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context == null || !context.Perception.HasTarget)
            return BehaviorNodeStatus.Failure;

        context.SyncBlackboardTarget();
        return BehaviorNodeStatus.Success;
    }
}

[FactoryKey(BehaviorNodeTypes.TargetInCastRange, 12, "Target In Cast Range")]
public sealed class TargetInCastRangeBehaviorNode : ConditionBehaviorNode
{
    private readonly TargetInCastRangeBehaviorNodeData _data;

    public TargetInCastRangeBehaviorNode(TargetInCastRangeBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context == null || !context.Perception.HasTarget)
            return BehaviorNodeStatus.Failure;

        if (!context.TryGetCastRange(out float castRange))
            return BehaviorNodeStatus.Failure;

        return context.Perception.TargetDistance <= math.max(0f, castRange + _data.RangePadding)
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Failure;
    }
}

[FactoryKey(BehaviorNodeTypes.MoveToTarget, 13, "Move To Target")]
public sealed class MoveToTargetBehaviorNode : ActionBehaviorNode
{
    private readonly MoveToTargetBehaviorNodeData _data;

    public MoveToTargetBehaviorNode(MoveToTargetBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context == null || !context.Perception.HasTarget)
            return BehaviorNodeStatus.Failure;

        if (!context.TryGetSelfPosition(out float2 selfPosition))
            return BehaviorNodeStatus.Failure;

        float2 targetPosition = context.Perception.TargetPosition;
        float2 toTarget = targetPosition - selfPosition;
        float distanceSq = math.lengthsq(toTarget);
        float stopDistance = math.max(0f, _data.StopDistance);
        if (distanceSq <= stopDistance * stopDistance)
        {
            context.SetMoveDirection(float2.zero);
            return BehaviorNodeStatus.Success;
        }

        float2 direction = math.normalizesafe(toTarget);
        context.SetMoveDirection(direction);
        context.SyncBlackboardTarget();
        return BehaviorNodeStatus.Running;
    }
}

[FactoryKey(BehaviorNodeTypes.CastToTarget, 14, "Cast To Target")]
public sealed class CastToTargetBehaviorNode : ActionBehaviorNode
{
    public CastToTargetBehaviorNode(CastToTargetBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context == null || !context.Perception.HasTarget)
            return BehaviorNodeStatus.Failure;

        context.SetCastTarget(context.Perception.TargetPosition);
        context.SetWantToCast();
        context.SyncBlackboardTarget();

        if (context.TryGetTargetEntity(out Entity targetEntity) && targetEntity != Entity.Null)
            return BehaviorNodeStatus.Running;

        return BehaviorNodeStatus.Success;
    }
}

[FactoryKey(BehaviorNodeTypes.Idle, 15, "Idle")]
public sealed class IdleBehaviorNode : ActionBehaviorNode
{
    public IdleBehaviorNode(IdleBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context != null)
            context.SetMoveDirection(float2.zero);

        return BehaviorNodeStatus.Running;
    }
}
