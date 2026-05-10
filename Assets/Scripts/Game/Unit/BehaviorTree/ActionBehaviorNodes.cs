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
    private readonly CastToTargetBehaviorNodeData _data;

    public CastToTargetBehaviorNode(CastToTargetBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context == null || !context.Perception.HasTarget)
            return BehaviorNodeStatus.Failure;

        context.SetCastTarget(context.Perception.TargetPosition);
        context.SetWantToCast();
        context.SyncBlackboardTarget();

        if (context.Entity != Entity.Null && context.EntityManager.HasComponent<UnitSkillComponent>(context.Entity))
        {
            UnitSkillComponent unitSkill = context.EntityManager.GetComponentData<UnitSkillComponent>(context.Entity);
            unitSkill.RequestMode = _data.SelectionMode;
            unitSkill.RequestedSkillId = _data.SkillId;
            unitSkill.RequestedTagMask = _data.SkillTagMask;
            context.EntityManager.SetComponentData(context.Entity, unitSkill);
        }

        if (context.TryGetTargetEntity(out Entity targetEntity) && targetEntity != Entity.Null)
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

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context == null)
            return BehaviorNodeStatus.Failure;

        if (_remainingSeconds <= 0f || math.lengthsq(_direction) <= 0.0001f)
            PickNextDirection();

        _remainingSeconds = math.max(0f, _remainingSeconds - math.max(0f, context.DeltaTime));
        context.SetMoveDirection(_direction);
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

    protected override BehaviorNodeStatus OnTick(BehaviorTreeContext context)
    {
        if (context != null)
            context.SetMoveDirection(float2.zero);

        return BehaviorNodeStatus.Running;
    }
}
