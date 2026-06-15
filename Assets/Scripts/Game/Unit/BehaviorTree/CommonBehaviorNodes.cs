using CrystalMagic.Game.Data;

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

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (Children.Count == 0 || Children[0] == null)
            return BehaviorNodeStatus.Failure;

        return Children[0].Tick(blackboard);
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

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        int startIndex = _runningChildIndex >= 0 ? _runningChildIndex : 0;
        for (int i = startIndex; i < Children.Count; i++)
        {
            BehaviorNodeStatus status = Children[i].Tick(blackboard);
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

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        int startIndex = _runningChildIndex >= 0 ? _runningChildIndex : 0;
        for (int i = startIndex; i < Children.Count; i++)
        {
            BehaviorNodeStatus status = Children[i].Tick(blackboard);
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

[FactoryKey(BehaviorNodeTypes.Parallel, 2, "Parallel")]
public sealed class ParallelBehaviorNode : CompositeBehaviorNode
{
    private readonly ParallelBehaviorNodeData _data;

    public ParallelBehaviorNode(ParallelBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (Children.Count == 0)
            return BehaviorNodeStatus.Failure;

        int successCount = 0;
        int failureCount = 0;
        int runningCount = 0;

        for (int i = 0; i < Children.Count; i++)
        {
            BehaviorNodeStatus status = Children[i].Tick(blackboard);
            switch (status)
            {
                case BehaviorNodeStatus.Success:
                    successCount++;
                    break;
                case BehaviorNodeStatus.Failure:
                    failureCount++;
                    break;
                case BehaviorNodeStatus.Running:
                    runningCount++;
                    break;
            }
        }

        if ((_data.FailurePolicy == ParallelFailurePolicy.RequireAny && failureCount > 0) ||
            (_data.FailurePolicy == ParallelFailurePolicy.RequireAll && failureCount == Children.Count))
        {
            return BehaviorNodeStatus.Failure;
        }

        if ((_data.SuccessPolicy == ParallelSuccessPolicy.RequireAny && successCount > 0) ||
            (_data.SuccessPolicy == ParallelSuccessPolicy.RequireAll && successCount == Children.Count))
        {
            return BehaviorNodeStatus.Success;
        }

        return runningCount > 0 || successCount > 0 || failureCount > 0
            ? BehaviorNodeStatus.Running
            : BehaviorNodeStatus.Failure;
    }
}

[FactoryKey(BehaviorNodeTypes.CheckCondition, 10, "Check Condition")]
public sealed class CheckConditionBehaviorNode : ABehaviorNode
{
    private static readonly ComparatorFactory s_comparatorFactory = CreateComparatorFactory();
    private readonly CheckConditionBehaviorNodeData _data;

    public CheckConditionBehaviorNode(CheckConditionBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorNodeStatus.Failure;

        Comparator comparator = s_comparatorFactory.BuildComparator(
            _data.Conditions,
            blackboard.Runtime.Entity,
            blackboard.Runtime.EntityManager);
        return comparator.GetResult()
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Failure;
    }

    private static ComparatorFactory CreateComparatorFactory()
    {
        var factory = new ComparatorFactory();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
