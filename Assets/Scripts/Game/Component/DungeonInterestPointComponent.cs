using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct DungeonInterestPointComponent : IComponentData
{
    public int EncounterId;
    public int SquadId;
    public float SpawnDistance;
    public float PatrolSpeed;
    public float ArrivalDistance;
    public byte PatrolEnabled;
    public Entity CurrentTarget;
    public int TargetCursor;
    public float NearestPlayerDistance;
    public int ActivePatrolMemberCount;
    public byte TargetReached;
}

[InternalBufferCapacity(4)]
public struct DungeonInterestPointCandidateElement : IBufferElementData
{
    public Entity Value;
}

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
        in DungeonInterestPointComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromInt(value.EncounterId),
            1 => UnitSourceValue.FromInt(value.SquadId),
            2 => UnitSourceValue.FromFloat(value.SpawnDistance),
            3 => UnitSourceValue.FromFloat(value.PatrolSpeed),
            4 => UnitSourceValue.FromFloat(value.ArrivalDistance),
            5 => UnitSourceValue.FromBool(value.ActivePatrolMemberCount > 0),
            6 => UnitSourceValue.FromEntity(value.CurrentTarget),
            7 => UnitSourceValue.FromFloat(value.NearestPlayerDistance),
            8 => UnitSourceValue.FromBool(
                value.PatrolEnabled != 0 &&
                value.ActivePatrolMemberCount < 1 &&
                (value.NearestPlayerDistance == float.MaxValue ||
                 value.NearestPlayerDistance >= math.max(0f, value.SpawnDistance))),
            9 => UnitSourceValue.FromBool(value.TargetReached != 0),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.interestPoint.setPatrolActive", UnitValueCategory.Bool, ParameterNames = new[] { "Value" })]
    [UnitSourceSet(1, "unit.interestPoint.setNextPatrolTarget", UnitValueCategory.Bool, ParameterNames = new[] { "Value" })]
    [UnitSourceSet(2, "unit.interestPoint.setPatrolSpeed", UnitValueCategory.Number, ParameterNames = new[] { "Value" })]
    public static bool TrySet(
        int operation,
        Entity entity,
        ref ComponentLookup<DungeonInterestPointComponent> componentLookup,
        ref BufferLookup<UnitVariableElement> variableLookup,
        in BufferLookup<DungeonInterestPointCandidateElement> candidateLookup,
        in BufferLookup<UnitVariableConsumerElement> consumerLookup,
        in ComponentLookup<UnitVariableComponent> unitVariableLookup,
        in ComponentLookup<LocalTransform> transformLookup,
        in ComponentLookup<DestroyEntityFlag> destroyLookup,
        in UnitSourceArguments arguments)
    {
        if (!componentLookup.TryGetComponent(entity, out DungeonInterestPointComponent value))
            return false;

        bool hasVariables = variableLookup.TryGetBuffer(
            entity,
            out DynamicBuffer<UnitVariableElement> variables);
        switch (operation)
        {
            case 0 when arguments.TryGetBool(0, out bool active):
                value.PatrolEnabled = active ? (byte)1 : (byte)0;
                componentLookup[entity] = value;
                if (hasVariables)
                {
                    UnitVariableSource.SetValue(
                        variables,
                        DungeonPatrolRuntimeUtility.PatrolActiveFixedKey,
                        UnitSourceValue.FromBool(active));
                }
                return true;
            case 1 when arguments.TryGetBool(0, out bool advance):
                if (!advance)
                    return true;
                if (!candidateLookup.TryGetBuffer(
                        entity,
                        out DynamicBuffer<DungeonInterestPointCandidateElement> candidates) ||
                    !TrySelectNextTarget(entity, candidates, in transformLookup, ref value))
                {
                    return false;
                }

                RefreshMemberState(
                    entity,
                    ref value,
                    in consumerLookup,
                    in unitVariableLookup,
                    in transformLookup,
                    in destroyLookup);
                componentLookup[entity] = value;
                if (hasVariables)
                    SetSharedPatrolValues(variables, in value);
                return true;
            case 2 when arguments.TryGetNumber(0, out float speed):
                value.PatrolSpeed = math.max(0f, speed);
                componentLookup[entity] = value;
                if (hasVariables)
                    SetSharedPatrolValues(variables, in value);
                return true;
            default:
                return false;
        }
    }

    public static void RefreshMemberState(
        Entity pointEntity,
        ref DungeonInterestPointComponent point,
        in BufferLookup<UnitVariableConsumerElement> consumerLookup,
        in ComponentLookup<UnitVariableComponent> unitVariableLookup,
        in ComponentLookup<LocalTransform> transformLookup,
        in ComponentLookup<DestroyEntityFlag> destroyLookup)
    {
        point.ActivePatrolMemberCount = 0;
        point.TargetReached = 0;
        if (!consumerLookup.TryGetBuffer(
                pointEntity,
                out DynamicBuffer<UnitVariableConsumerElement> consumers))
        {
            return;
        }

        LocalTransform targetTransform = default;
        bool hasTargetTransform = point.CurrentTarget != Entity.Null &&
                                  transformLookup.TryGetComponent(
                                      point.CurrentTarget,
                                      out targetTransform);
        float arrivalDistance = math.max(0.05f, point.ArrivalDistance);
        float arrivalDistanceSq = arrivalDistance * arrivalDistance;
        bool hasPositionedMember = false;
        bool allPositionedMembersReached = true;
        for (int index = 0; index < consumers.Length; index++)
        {
            Entity member = consumers[index].Value;
            if (!unitVariableLookup.TryGetComponent(member, out UnitVariableComponent variables) ||
                variables.Other != pointEntity ||
                destroyLookup.HasComponent(member) && destroyLookup.IsComponentEnabled(member))
            {
                continue;
            }

            point.ActivePatrolMemberCount++;
            if (!hasTargetTransform || !transformLookup.TryGetComponent(member, out LocalTransform memberTransform))
                continue;

            hasPositionedMember = true;
            if (math.lengthsq(targetTransform.Position.xy - memberTransform.Position.xy) > arrivalDistanceSq)
                allPositionedMembersReached = false;
        }

        if (point.ActivePatrolMemberCount < 1)
            return;

        if (point.CurrentTarget == Entity.Null)
        {
            point.TargetReached = 1;
            return;
        }

        point.TargetReached = hasTargetTransform && hasPositionedMember && allPositionedMembersReached
            ? (byte)1
            : (byte)0;
    }

    private static bool TrySelectNextTarget(
        Entity pointEntity,
        in DynamicBuffer<DungeonInterestPointCandidateElement> candidates,
        in ComponentLookup<LocalTransform> transformLookup,
        ref DungeonInterestPointComponent point)
    {
        if (candidates.Length == 0)
            return false;

        int startIndex = math.clamp(point.TargetCursor, 0, candidates.Length - 1);
        for (int offset = 0; offset < candidates.Length; offset++)
        {
            int index = (startIndex + offset) % candidates.Length;
            Entity target = candidates[index].Value;
            if (target == Entity.Null || target == pointEntity || !transformLookup.HasComponent(target))
                continue;

            point.TargetCursor = (index + 1) % candidates.Length;
            point.CurrentTarget = target;
            return true;
        }

        return false;
    }

    private static void SetSharedPatrolValues(
        DynamicBuffer<UnitVariableElement> variables,
        in DungeonInterestPointComponent point)
    {
        UnitVariableSource.SetValue(
            variables,
            DungeonPatrolRuntimeUtility.PatrolActiveFixedKey,
            UnitSourceValue.FromBool(point.PatrolEnabled != 0));
        UnitVariableSource.SetValue(
            variables,
            DungeonPatrolRuntimeUtility.PatrolTargetFixedKey,
            UnitSourceValue.FromEntity(point.CurrentTarget));
        UnitVariableSource.SetValue(
            variables,
            DungeonPatrolRuntimeUtility.PatrolSpeedFixedKey,
            UnitSourceValue.FromFloat(math.max(0f, point.PatrolSpeed)));
        UnitVariableSource.SetValue(
            variables,
            DungeonPatrolRuntimeUtility.PatrolArrivalDistanceFixedKey,
            UnitSourceValue.FromFloat(math.max(0.05f, point.ArrivalDistance)));
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

    public static readonly FixedString128Bytes PatrolActiveFixedKey = PatrolActiveKey;
    public static readonly FixedString128Bytes PatrolTargetFixedKey = PatrolTargetKey;
    public static readonly FixedString128Bytes PatrolSpeedFixedKey = PatrolSpeedKey;
    public static readonly FixedString128Bytes PatrolArrivalDistanceFixedKey = PatrolArrivalDistanceKey;

    public static void SetSharedPatrolValues(
        EntityManager entityManager,
        Entity pointEntity,
        in DungeonInterestPointComponent point)
    {
        UnitVariableSource.TrySetValue(
            entityManager,
            pointEntity,
            PatrolActiveKey,
            UnitValue.FromBool(point.PatrolEnabled != 0));
        UnitVariableSource.TrySetValue(
            entityManager,
            pointEntity,
            PatrolTargetKey,
            UnitValue.FromEntity(point.CurrentTarget));
        UnitVariableSource.TrySetValue(
            entityManager,
            pointEntity,
            PatrolSpeedKey,
            UnitValue.FromFloat(math.max(0f, point.PatrolSpeed)));
        UnitVariableSource.TrySetValue(
            entityManager,
            pointEntity,
            PatrolArrivalDistanceKey,
            UnitValue.FromFloat(math.max(0.05f, point.ArrivalDistance)));
    }
}
