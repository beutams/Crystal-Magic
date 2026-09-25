using Unity.Entities;
using Unity.Mathematics;

public static class DungeonExitRuntimeUtility
{
    private const string TargetThemeIdKey = "dungeon.exit.targetThemeId";
    private const string TargetFloorKey = "dungeon.exit.targetFloor";

    public static void SetDestination(
        EntityManager entityManager,
        Entity entity,
        int targetThemeId,
        int targetFloor)
    {
        if (!entityManager.HasComponent<UnitVariableComponent>(entity))
            entityManager.AddComponentData(entity, new UnitVariableComponent { Other = Entity.Null });
        if (!entityManager.HasBuffer<UnitVariableElement>(entity))
            entityManager.AddBuffer<UnitVariableElement>(entity);
        if (!entityManager.HasBuffer<UnitVariableConsumerElement>(entity))
            entityManager.AddBuffer<UnitVariableConsumerElement>(entity);

        UnitVariableSource.TrySetValue(
            entityManager,
            entity,
            TargetThemeIdKey,
            UnitValue.FromInt(targetThemeId));
        UnitVariableSource.TrySetValue(
            entityManager,
            entity,
            TargetFloorKey,
            UnitValue.FromInt(math.max(1, targetFloor)));
    }

    public static bool TryGetDestination(
        EntityManager entityManager,
        Entity entity,
        out int targetThemeId,
        out int targetFloor)
    {
        targetThemeId = -1;
        targetFloor = 1;
        if (!UnitVariableSource.TryGetValue(
                entityManager,
                entity,
                TargetThemeIdKey,
                out UnitSourceValue themeValue) ||
            !themeValue.TryGetNumber(out float themeNumber) ||
            !UnitVariableSource.TryGetValue(
                entityManager,
                entity,
                TargetFloorKey,
                out UnitSourceValue floorValue) ||
            !floorValue.TryGetNumber(out float floorNumber))
        {
            return false;
        }

        targetThemeId = (int)math.round(themeNumber);
        targetFloor = math.max(1, (int)math.round(floorNumber));
        return true;
    }
}
