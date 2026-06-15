using System.Collections.Generic;
using CrystalMagic.Game.Data;

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

    public BehaviorNodeStatus Tick(BehaviorBlackboard blackboard)
    {
        blackboard?.SetCurrentNode(this);
        return OnTick(blackboard);
    }

    public virtual void Reset()
    {
        for (int i = 0; i < Children.Count; i++)
            Children[i]?.Reset();
    }

    protected abstract BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard);
}

public abstract class CompositeBehaviorNode : ABehaviorNode
{
    protected CompositeBehaviorNode(BehaviorNodeData data)
        : base(data)
    {
    }
}

public abstract class DecoratorBehaviorNode : ABehaviorNode
{
    protected DecoratorBehaviorNode(BehaviorNodeData data)
        : base(data)
    {
    }

    protected ABehaviorNode Child => Children.Count > 0 ? Children[0] : null;

    public override void AddChild(ABehaviorNode child)
    {
        if (child == null)
            return;

        Children.Clear();
        Children.Add(child);
    }
}

public abstract class ActionBehaviorNode : ABehaviorNode
{
    protected ActionBehaviorNode(BehaviorNodeData data)
        : base(data)
    {
    }
}
