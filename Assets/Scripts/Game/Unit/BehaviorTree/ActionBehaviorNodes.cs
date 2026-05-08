using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

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
