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
    public int AliveGuardCount;
    public int PatrolUnitCount;
    public Entity PatrolTarget;
    public byte PatrolEnabled;
    public byte EncounterReady;
}

public struct DungeonRuntimeOwnedEntity : IComponentData
{
}

public struct DungeonFloorControllerComponent : IComponentData
{
}

public struct DungeonWildSquadComponent : IComponentData
{
    public int SquadId;
}

[UnitSourceProvider(typeof(DungeonInterestPointComponent), typeof(DungeonInterestPointAuthoring))]
public static class DungeonInterestPointSource
{
    [UnitSourceGet(0, "unit.interestPoint.encounterId", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.interestPoint.squadId", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.interestPoint.spawnDistance", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.interestPoint.patrolSpeed", UnitValueCategory.Number)]
    [UnitSourceGet(4, "unit.interestPoint.arrivalDistance", UnitValueCategory.Number)]
    [UnitSourceGet(5, "unit.interestPoint.aliveGuardCount", UnitValueCategory.Number)]
    [UnitSourceGet(6, "unit.interestPoint.patrolUnitCount", UnitValueCategory.Number)]
    [UnitSourceGet(7, "unit.interestPoint.patrolTarget", UnitValueCategory.Entity)]
    [UnitSourceGet(8, "unit.interestPoint.encounterReady", UnitValueCategory.Bool)]
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
            5 => UnitSourceValue.FromInt(value.AliveGuardCount),
            6 => UnitSourceValue.FromInt(value.PatrolUnitCount),
            7 => UnitSourceValue.FromEntity(value.PatrolTarget),
            8 => UnitSourceValue.FromBool(value.EncounterReady != 0),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.interestPoint.setPatrolTarget", UnitValueCategory.Entity,
        ParameterNames = new[] { "Target" })]
    public static bool TrySet(
        int operation,
        ref DungeonInterestPointComponent value,
        in UnitSourceArguments arguments)
    {
        if (operation != 0 || !arguments.TryGetEntity(0, out Entity target))
            return false;

        value.PatrolTarget = target;
        return true;
    }
}

public static class DungeonInterestPointUtility
{
    public static bool AttachMember(
        EntityManager entityManager,
        Entity member,
        Entity interestPoint,
        bool countsAsGuard = false,
        bool countsAsPatrol = false)
    {
        if (!entityManager.Exists(member) ||
            !entityManager.Exists(interestPoint) ||
            !entityManager.HasComponent<DungeonInterestPointComponent>(interestPoint))
        {
            return false;
        }

        if (!entityManager.HasComponent<UnitVariableComponent>(member))
            entityManager.AddComponentData(member, new UnitVariableComponent { Other = Entity.Null });
        if (!entityManager.HasBuffer<UnitVariableElement>(member))
            entityManager.AddBuffer<UnitVariableElement>(member);
        if (!entityManager.HasBuffer<UnitVariableConsumerElement>(member))
            entityManager.AddBuffer<UnitVariableConsumerElement>(member);

        bool hasMonsterData = entityManager.HasComponent<DungeonMonsterSpawnComponent>(member);
        DungeonMonsterSpawnComponent monster = hasMonsterData
            ? entityManager.GetComponentData<DungeonMonsterSpawnComponent>(member)
            : default;
        Entity previousOwner = UnitVariableSource.GetOther(entityManager, member);
        if (previousOwner != interestPoint && hasMonsterData)
        {
            RemoveCounts(entityManager, previousOwner, monster.CountsAsGuard != 0, monster.CountsAsPatrol != 0);
            monster.CountsAsGuard = 0;
            monster.CountsAsPatrol = 0;
        }

        if (!UnitVariableSource.SetOther(entityManager, member, interestPoint))
            return false;

        UnitOwnerComponent owner = new() { Owner = interestPoint };
        if (entityManager.HasComponent<UnitOwnerComponent>(member))
            entityManager.SetComponentData(member, owner);
        else
            entityManager.AddComponentData(member, owner);

        if (!hasMonsterData)
            return true;

        DungeonInterestPointComponent point = entityManager.GetComponentData<DungeonInterestPointComponent>(interestPoint);
        if (countsAsGuard && monster.CountsAsGuard == 0)
        {
            monster.CountsAsGuard = 1;
            point.AliveGuardCount++;
        }
        if (countsAsPatrol && monster.CountsAsPatrol == 0)
        {
            monster.CountsAsPatrol = 1;
            point.PatrolUnitCount++;
        }

        entityManager.SetComponentData(member, monster);
        entityManager.SetComponentData(interestPoint, point);
        UnitVariableSource.TrySetValue(entityManager, member, DungeonPatrolRuntimeUtility.GuardMemberKey,
            UnitValue.FromBool(monster.CountsAsGuard != 0));
        UnitVariableSource.TrySetValue(entityManager, member, DungeonPatrolRuntimeUtility.PatrolMemberKey,
            UnitValue.FromBool(monster.CountsAsPatrol != 0));
        if (monster.CountsAsGuard != 0)
            UnitVariableSource.TrySetValue(entityManager, interestPoint, "dungeon.encounter.populated", UnitValue.FromBool(true));
        return true;
    }

    public static void RemoveDeadMember(EntityManager entityManager, Entity member)
    {
        if (!entityManager.Exists(member) ||
            !entityManager.HasComponent<DungeonMonsterSpawnComponent>(member))
        {
            return;
        }

        DungeonMonsterSpawnComponent monster = entityManager.GetComponentData<DungeonMonsterSpawnComponent>(member);
        if (monster.CountsAsGuard == 0 && monster.CountsAsPatrol == 0)
            return;

        RemoveCounts(
            entityManager,
            entityManager.HasComponent<UnitOwnerComponent>(member)
                ? entityManager.GetComponentData<UnitOwnerComponent>(member).Owner
                : Entity.Null,
            monster.CountsAsGuard != 0,
            monster.CountsAsPatrol != 0);
        monster.CountsAsGuard = 0;
        monster.CountsAsPatrol = 0;
        entityManager.SetComponentData(member, monster);
    }

    private static void RemoveCounts(
        EntityManager entityManager,
        Entity interestPoint,
        bool removeGuard,
        bool removePatrol)
    {
        if (interestPoint == Entity.Null ||
            !entityManager.Exists(interestPoint) ||
            !entityManager.HasComponent<DungeonInterestPointComponent>(interestPoint))
        {
            return;
        }

        DungeonInterestPointComponent point = entityManager.GetComponentData<DungeonInterestPointComponent>(interestPoint);
        if (removeGuard)
            point.AliveGuardCount = math.max(0, point.AliveGuardCount - 1);
        if (removePatrol)
            point.PatrolUnitCount = math.max(0, point.PatrolUnitCount - 1);
        entityManager.SetComponentData(interestPoint, point);

        // The parent state script handles the transition to cleared and threat settlement.
    }
}

public static class DungeonPatrolRuntimeUtility
{
    public const int InterestPointUnitDataId = 30;
    public const int FloorControllerUnitDataId = 35;
    public const int WildSquadUnitDataId = 36;
    public const string GuardMemberKey = "dungeon.encounter.guard";
    public const string PatrolMemberKey = "dungeon.patrol.member";
    public const string EncounterDeadKey = "dungeon.encounter.dead";
    public const string PatrolActiveKey = "dungeon.patrol.active";
    public const string PatrolSpeedKey = "dungeon.patrol.speed";
    public const string PatrolArrivalDistanceKey = "dungeon.patrol.arrivalDistance";
    public const string PatrolSpawnListKey = "dungeon.patrol.spawn";
    public const string InterestPointStateKey = "dungeon.interestPoint.state";

    public static readonly FixedString128Bytes PatrolActiveFixedKey = PatrolActiveKey;
    public static readonly FixedString128Bytes PatrolSpeedFixedKey = PatrolSpeedKey;
    public static readonly FixedString128Bytes PatrolArrivalDistanceFixedKey = PatrolArrivalDistanceKey;

    public static bool IsEncounterDead(EntityManager entityManager, Entity entity)
    {
        return UnitVariableSource.TryGetValue(entityManager, entity, EncounterDeadKey, out UnitSourceValue value) &&
               value.TryGetBool(out bool dead) && dead;
    }

    public static void InitializeEncounterVariables(EntityManager entityManager, Entity entity, float clearThreat)
    {
        UnitVariableSource.TrySetValue(entityManager, entity, EncounterDeadKey, UnitValue.FromBool(false));
        UnitVariableSource.TrySetValue(entityManager, entity, "dungeon.encounter.populated", UnitValue.FromBool(false));
        UnitVariableSource.TrySetValue(entityManager, entity, "dungeon.encounter.ready", UnitValue.FromBool(false));
        UnitVariableSource.TrySetValue(entityManager, entity, "dungeon.threat.clearSettled", UnitValue.FromBool(false));
        UnitVariableSource.TrySetValue(entityManager, entity, "dungeon.threat.onClear", UnitValue.FromFloat(clearThreat));
    }

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
            PatrolSpeedKey,
            UnitValue.FromFloat(math.max(0f, point.PatrolSpeed)));
        UnitVariableSource.TrySetValue(
            entityManager,
            pointEntity,
            PatrolArrivalDistanceKey,
            UnitValue.FromFloat(math.max(0.05f, point.ArrivalDistance)));
    }
}
