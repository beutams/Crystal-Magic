using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Runtime-only unit representing one open-field interest point.
/// The point owns the patrol squad's shared variables and is driven by the
/// DungeonInterestPoint state script.
/// </summary>
public sealed class DungeonInterestPointComponent : IComponentData
{
    public int EncounterId;
    public int SquadId;
    public float SpawnDistance;
    public float PatrolSpeed;
    public float ArrivalDistance;
    public bool PatrolEnabled = true;
    public Entity CurrentTarget = Entity.Null;
    public int TargetCursor;
    public List<Entity> CandidateTargets = new();
}

/// <summary>
/// Marks entities created by the runtime dungeon builder that are not part of
/// the initial tracked spawn list (for example, lazy patrol members).
/// </summary>
public struct DungeonRuntimeOwnedEntity : IComponentData
{
}

[UnitSourceProvider(typeof(DungeonInterestPointComponent), typeof(DungeonInterestPointAuthoring))]
public static class DungeonInterestPointSource
{
    [UnitSourceGet(0, "unit.interestPoint.encounterId", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.interestPoint.squadId", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.interestPoint.spawnDistance", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.interestPoint.patrolSpeed", UnitValueCategory.Number)]
    [UnitSourceGet(4, "unit.interestPoint.arrivalDistance", UnitValueCategory.Number)]
    [UnitSourceGet(5, "unit.interestPoint.hasPatrol", UnitValueCategory.Bool)]
    [UnitSourceGet(6, "unit.interestPoint.currentTarget", UnitValueCategory.Entity)]
    [UnitSourceGet(7, "unit.interestPoint.playerDistance", UnitValueCategory.Number)]
    [UnitSourceGet(8, "unit.interestPoint.shouldSpawnPatrol", UnitValueCategory.Bool)]
    [UnitSourceGet(9, "unit.interestPoint.targetReached", UnitValueCategory.Bool)]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<DungeonInterestPointComponent>(entity))
            return false;

        DungeonInterestPointComponent value = entityManager.GetComponentObject<DungeonInterestPointComponent>(entity);
        if (value == null)
            return false;

        result = operation switch
        {
            0 => UnitSourceValue.FromInt(value.EncounterId),
            1 => UnitSourceValue.FromInt(value.SquadId),
            2 => UnitSourceValue.FromFloat(value.SpawnDistance),
            3 => UnitSourceValue.FromFloat(value.PatrolSpeed),
            4 => UnitSourceValue.FromFloat(value.ArrivalDistance),
            5 => UnitSourceValue.FromBool(UnitVariableSource.CountConsumers(entityManager, entity) > 0),
            6 => UnitSourceValue.FromEntity(value.CurrentTarget),
            7 => UnitSourceValue.FromFloat(DungeonPatrolRuntimeUtility.GetNearestPlayerDistance(entityManager, entity)),
            8 => UnitSourceValue.FromBool(value.PatrolEnabled && UnitVariableSource.CountConsumers(entityManager, entity) < 1 &&
                                          DungeonPatrolRuntimeUtility.IsFarFromPlayer(entityManager, entity, value.SpawnDistance)),
            9 => UnitSourceValue.FromBool(DungeonPatrolRuntimeUtility.IsTargetReached(entityManager, entity, value)),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.interestPoint.setPatrolActive", UnitValueCategory.Bool, ParameterNames = new[] { "Value" })]
    [UnitSourceSet(1, "unit.interestPoint.setNextPatrolTarget", UnitValueCategory.Bool, ParameterNames = new[] { "Value" })]
    [UnitSourceSet(2, "unit.interestPoint.setPatrolSpeed", UnitValueCategory.Number, ParameterNames = new[] { "Value" })]
    public static bool TrySet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments)
    {
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<DungeonInterestPointComponent>(entity))
            return false;

        DungeonInterestPointComponent value = entityManager.GetComponentObject<DungeonInterestPointComponent>(entity);
        if (value == null)
            return false;

        switch (operation)
        {
            case 0 when arguments.TryGetBool(0, out bool active):
                value.PatrolEnabled = active;
                DungeonPatrolRuntimeUtility.SetSharedPatrolActive(entityManager, entity, active);
                return true;
            case 1 when arguments.TryGetBool(0, out bool advance):
                return !advance || DungeonPatrolRuntimeUtility.TrySelectNextTarget(entityManager, entity, value);
            case 2 when arguments.TryGetNumber(0, out float speed):
                value.PatrolSpeed = math.max(0f, speed);
                DungeonPatrolRuntimeUtility.SetSharedPatrolValues(entityManager, entity, value);
                return true;
            default:
                return false;
        }
    }
}

public static class DungeonPatrolRuntimeUtility
{
    public const int InterestPointUnitDataId = 30;
    public const string PatrolActiveKey = "dungeon.patrol.active";
    public const string PatrolTargetKey = "dungeon.patrol.target";
    public const string PatrolSpeedKey = "dungeon.patrol.speed";
    public const string PatrolArrivalDistanceKey = "dungeon.patrol.arrivalDistance";
    public const string PatrolSpawnListKey = "dungeon.patrol.spawn";
    public const string InterestPointStateKey = "dungeon.interestPoint.state";

    public static bool TrySelectNextTarget(
        EntityManager entityManager,
        Entity pointEntity,
        DungeonInterestPointComponent point)
    {
        if (point == null || point.CandidateTargets == null || point.CandidateTargets.Count == 0)
            return false;

        int candidateCount = point.CandidateTargets.Count;
        int startIndex = math.clamp(point.TargetCursor, 0, candidateCount - 1);
        for (int offset = 0; offset < candidateCount; offset++)
        {
            int index = (startIndex + offset) % candidateCount;
            Entity target = point.CandidateTargets[index];
            if (target == Entity.Null || target == pointEntity ||
                !entityManager.Exists(target) || !entityManager.HasComponent<LocalTransform>(target))
            {
                continue;
            }

            point.TargetCursor = (index + 1) % candidateCount;
            point.CurrentTarget = target;
            SetSharedPatrolValues(entityManager, pointEntity, point);
            return true;
        }

        return false;
    }

    public static bool IsTargetReached(
        EntityManager entityManager,
        Entity pointEntity,
        DungeonInterestPointComponent point)
    {
        if (point == null || pointEntity == Entity.Null || !entityManager.Exists(pointEntity))
            return false;

        if (UnitVariableSource.CountConsumers(entityManager, pointEntity) < 1)
            return false;

        if (point.CurrentTarget == Entity.Null)
            return true;

        if (!entityManager.Exists(point.CurrentTarget) ||
            !entityManager.HasComponent<LocalTransform>(point.CurrentTarget))
        {
            return false;
        }

        float2 targetPosition = entityManager.GetComponentData<LocalTransform>(point.CurrentTarget).Position.xy;
        float arrivalDistance = math.max(0.05f, point.ArrivalDistance);
        float arrivalDistanceSq = arrivalDistance * arrivalDistance;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitVariableComponent>(),
            ComponentType.ReadOnly<LocalTransform>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        bool hasMember = false;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity member = entities[index];
            UnitVariableComponent variables = entityManager.GetComponentObject<UnitVariableComponent>(member);
            if (variables?.Owner != pointEntity ||
                entityManager.HasComponent<DestroyEntityFlag>(member) &&
                entityManager.IsComponentEnabled<DestroyEntityFlag>(member))
            {
                continue;
            }

            hasMember = true;
            float2 memberPosition = entityManager.GetComponentData<LocalTransform>(member).Position.xy;
            if (math.lengthsq(targetPosition - memberPosition) > arrivalDistanceSq)
                return false;
        }

        return hasMember;
    }

    public static bool IsFarFromPlayer(EntityManager entityManager, Entity pointEntity, float distance)
    {
        float nearestDistance = GetNearestPlayerDistance(entityManager, pointEntity);
        return nearestDistance == float.MaxValue || nearestDistance >= math.max(0f, distance);
    }

    public static float GetNearestPlayerDistance(EntityManager entityManager, Entity pointEntity)
    {
        if (pointEntity == Entity.Null || !entityManager.Exists(pointEntity) ||
            !entityManager.HasComponent<LocalTransform>(pointEntity))
        {
            return float.MaxValue;
        }

        float2 pointPosition = entityManager.GetComponentData<LocalTransform>(pointEntity).Position.xy;
        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<UnitFactionComponent>(),
            ComponentType.ReadOnly<LocalTransform>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

        float nearestDistanceSq = float.MaxValue;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity candidate = entities[index];
            if (candidate == pointEntity ||
                entityManager.GetComponentData<UnitFactionComponent>(candidate).Value != UnitFactionType.Player)
            {
                continue;
            }

            float2 candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position.xy;
            nearestDistanceSq = math.min(nearestDistanceSq, math.lengthsq(candidatePosition - pointPosition));
        }

        return nearestDistanceSq == float.MaxValue ? float.MaxValue : math.sqrt(nearestDistanceSq);
    }

    public static void SetSharedPatrolActive(EntityManager entityManager, Entity pointEntity, bool active)
    {
        if (!TryGetVariables(entityManager, pointEntity, out UnitVariableComponent variables))
            return;

        SetValue(variables, PatrolActiveKey, UnitValue.FromBool(active));
    }

    public static void SetSharedPatrolValues(
        EntityManager entityManager,
        Entity pointEntity,
        DungeonInterestPointComponent point)
    {
        if (point == null || !TryGetVariables(entityManager, pointEntity, out UnitVariableComponent variables))
            return;

        SetValue(variables, PatrolActiveKey, UnitValue.FromBool(point.PatrolEnabled));
        SetValue(variables, PatrolTargetKey, UnitValue.FromEntity(point.CurrentTarget));
        SetValue(variables, PatrolSpeedKey, UnitValue.FromFloat(math.max(0f, point.PatrolSpeed)));
        SetValue(variables, PatrolArrivalDistanceKey, UnitValue.FromFloat(math.max(0.05f, point.ArrivalDistance)));
    }

    private static bool TryGetVariables(EntityManager entityManager, Entity entity, out UnitVariableComponent variables)
    {
        variables = null;
        if (entity == Entity.Null || !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitVariableComponent>(entity))
        {
            return false;
        }

        variables = entityManager.GetComponentObject<UnitVariableComponent>(entity);
        return variables != null;
    }

    private static void SetValue(UnitVariableComponent variables, string key, UnitValue value)
    {
        variables.Values ??= new Dictionary<string, UnitValue>(StringComparer.Ordinal);
        variables.Values[key] = value;
    }

}
