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

    public BehaviorNodeStatus Tick(BehaviorContext context)
    {
        context?.SetCurrentNode(this);
        return OnTick(context);
    }

    public bool TryBind(UnitSourceAccessTable sources, out string error)
    {
        if (sources == null)
        {
            error = $"{DisplayName} requires a unit source table.";
            return false;
        }

        if (!OnBind(sources, out error))
            return false;

        for (int i = 0; i < Children.Count; i++)
        {
            ABehaviorNode child = Children[i];
            if (child == null)
            {
                error = $"{DisplayName} has an empty child at index {i}.";
                return false;
            }

            if (!child.TryBind(sources, out error))
                return false;
        }

        error = string.Empty;
        return true;
    }

    public virtual void Reset()
    {
        for (int i = 0; i < Children.Count; i++)
            Children[i]?.Reset();
    }

    protected virtual bool OnBind(UnitSourceAccessTable sources, out string error)
    {
        error = string.Empty;
        return true;
    }

    protected abstract BehaviorNodeStatus OnTick(BehaviorContext context);
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
