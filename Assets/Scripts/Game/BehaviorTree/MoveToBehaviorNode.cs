using System;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[FactoryKey(BehaviorNodeTypes.MoveTo, 14, "Move To")]
public sealed class MoveToBehaviorNode : ActionBehaviorNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly MoveToBehaviorNodeData _data;
    private Comparator _conditions;
    private Func<UnitValue> _destinationGetter;
    private Func<UnitValue> _stopDistanceGetter;
    private Func<UnitValue> _speedGetter;

    public MoveToBehaviorNode(MoveToBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override bool OnBind(UnitSourceAccessTable sources, out string error)
    {
        _data.Conditions ??= new System.Collections.Generic.List<ConditionConfig>();
        _data.Destination ??= MoveToBehaviorNodeData.CreateDefaultDestination();
        _data.StopDistance ??= MoveToBehaviorNodeData.CreateDefaultStopDistance();
        _data.Speed ??= MoveToBehaviorNodeData.CreateDefaultSpeed();

        _conditions = s_expressionFactory.BuildComparator(_data.Conditions, sources);
        if (_conditions == null || !_conditions.IsValid)
        {
            error = "MoveTo contains an invalid condition.";
            return false;
        }

        if (!TryBindExpression(_data.Destination, UnitValueCategory.Float3, sources, out _destinationGetter, out error) ||
            !TryBindExpression(_data.StopDistance, UnitValueCategory.Number, sources, out _stopDistanceGetter, out error) ||
            !TryBindExpression(_data.Speed, UnitValueCategory.Number, sources, out _speedGetter, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorContext context)
    {
        if (context == null || _conditions == null || !_conditions.GetResult() ||
            _destinationGetter == null || !_destinationGetter().TryGetFloat3(out float3 destination) ||
            _stopDistanceGetter == null || !_stopDistanceGetter().TryGetNumber(out float stopDistance) ||
            _speedGetter == null || !_speedGetter().TryGetNumber(out float speed))
        {
            return BehaviorNodeStatus.Failure;
        }

        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitNavigationComponent>(entity) ||
            !entityManager.HasComponent<UnitMoveComponent>(entity) ||
            !entityManager.HasComponent<LocalTransform>(entity))
        {
            return BehaviorNodeStatus.Failure;
        }

        UnitNavigationComponent navigation = entityManager.GetComponentData<UnitNavigationComponent>(entity);
        UnitNavigationUtility.SetDestination(ref navigation, destination, math.max(0f, stopDistance));
        entityManager.SetComponentData(entity, navigation);

        UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
        float commandSpeed = speed < 0f ? -1f : math.max(0f, speed);
        if (move.CommandMoveSpeed != commandSpeed)
        {
            move.CommandMoveSpeed = commandSpeed;
            move.NetworkDirty = 1;
            entityManager.SetComponentData(entity, move);
        }

        float3 position = entityManager.GetComponentData<LocalTransform>(entity).Position;
        float arrivalDistance = math.max(0f, stopDistance);
        return math.distancesq(position.xy, destination.xy) <= arrivalDistance * arrivalDistance
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Running;
    }

    private static bool TryBindExpression(
        ValueExpression expression,
        UnitValueCategory expectedCategory,
        UnitSourceAccessTable sources,
        out Func<UnitValue> getter,
        out string error)
    {
        getter = null;
        if (!s_expressionFactory.TryBuildValueExpression(
                expression,
                sources,
                out UnitValueCategory category,
                out getter,
                out error))
        {
            return false;
        }

        if (category == expectedCategory)
            return true;

        error = $"MoveTo requires {expectedCategory}, but received {category}.";
        getter = null;
        return false;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
