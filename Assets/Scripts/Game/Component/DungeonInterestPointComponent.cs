using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct DungeonInterestPointComponent : IComponentData
{
    public int EncounterId;
    public int SquadId;
    public float SpawnDistance;
    public float PatrolSpeed;
    public float ArrivalDistance;
    public byte PatrolEnabled;
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
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
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
            UnitValue.FromEntity(Entity.Null));
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
