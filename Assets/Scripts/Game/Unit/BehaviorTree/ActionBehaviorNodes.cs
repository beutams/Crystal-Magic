using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[FactoryKey(BehaviorNodeTypes.MoveToTarget, 13, "Move To Target")]
public sealed class MoveToTargetBehaviorNode : ActionBehaviorNode
{
    private readonly MoveToTargetBehaviorNodeData _data;

    public MoveToTargetBehaviorNode(MoveToTargetBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.Sense.HasTarget)
            return BehaviorNodeStatus.Failure;

        if (!blackboard.TryGetSelfPosition(out float2 selfPosition))
            return BehaviorNodeStatus.Failure;

        float2 targetPosition = blackboard.Sense.TargetPosition;
        float2 toTarget = targetPosition - selfPosition;
        float distanceSq = math.lengthsq(toTarget);
        float stopDistance = math.max(0f, _data.StopDistance);
        if (distanceSq <= stopDistance * stopDistance)
        {
            blackboard.SetMoveDirection(float2.zero);
            return BehaviorNodeStatus.Success;
        }

        float2 direction = math.normalizesafe(toTarget);
        blackboard.SetMoveDirection(direction);
        return BehaviorNodeStatus.Running;
    }
}

[FactoryKey(BehaviorNodeTypes.CastToTarget, 14, "Cast To Target")]
public sealed class CastToTargetBehaviorNode : ActionBehaviorNode
{
    private readonly CastToTargetBehaviorNodeData _data;

    public CastToTargetBehaviorNode(CastToTargetBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.Sense.HasTarget)
            return BehaviorNodeStatus.Failure;

        blackboard.SetCastTarget(blackboard.Sense.TargetPosition);
        blackboard.SetWantToCast();
        blackboard.SetSkillRequest(_data.SelectionMode, _data.SkillId, _data.SkillTagMask);

        if (blackboard.TryGetTargetEntity(out Entity targetEntity) && targetEntity != Entity.Null)
            return BehaviorNodeStatus.Running;

        return BehaviorNodeStatus.Success;
    }
}

 [FactoryKey(BehaviorNodeTypes.Wander, 15, "Wander")]
public sealed class WanderBehaviorNode : ActionBehaviorNode
{
    private readonly WanderBehaviorNodeData _data;
    private float _remainingSeconds;
    private float2 _direction;

    public WanderBehaviorNode(WanderBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (blackboard == null)
            return BehaviorNodeStatus.Failure;

        if (_remainingSeconds <= 0f || math.lengthsq(_direction) <= 0.0001f)
            PickNextDirection();

        _remainingSeconds = math.max(0f, _remainingSeconds - math.max(0f, blackboard.Runtime.DeltaTime));
        blackboard.SetMoveDirection(_direction);
        return BehaviorNodeStatus.Running;
    }

    public override void Reset()
    {
        _remainingSeconds = 0f;
        _direction = float2.zero;
        base.Reset();
    }

    private void PickNextDirection()
    {
        float minDuration = math.max(0.1f, _data.MinDurationSeconds);
        float maxDuration = math.max(minDuration, _data.MaxDurationSeconds);
        _remainingSeconds = UnityEngine.Random.Range(minDuration, maxDuration);

        Vector2 random = UnityEngine.Random.insideUnitCircle;
        if (random.sqrMagnitude <= 0.0001f)
            random = Vector2.right;

        _direction = math.normalizesafe(new float2(random.x, random.y), new float2(1f, 0f));
    }
}

[FactoryKey(BehaviorNodeTypes.Idle, 16, "Idle")]
public sealed class IdleBehaviorNode : ActionBehaviorNode
{
    public IdleBehaviorNode(IdleBehaviorNodeData data)
        : base(data)
    {
    }

    protected override BehaviorNodeStatus OnTick(BehaviorBlackboard blackboard)
    {
        if (blackboard != null)
            blackboard.SetMoveDirection(float2.zero);

        return BehaviorNodeStatus.Running;
    }
}
