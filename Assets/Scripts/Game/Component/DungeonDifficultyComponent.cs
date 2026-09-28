using Unity.Entities;
using Unity.Mathematics;

/// <summary>Floor-local difficulty snapshot, inherited by encounter parents and their spawned units.</summary>
public struct DungeonDifficultyComponent : IComponentData
{
    public int Floor;
    public float HealthMultiplier;
    public float BudgetMultiplier;
    public byte HealthApplied;

    public static DungeonDifficultyComponent Identity => new()
    {
        Floor = 1, HealthMultiplier = 1f, BudgetMultiplier = 1f,
    };
}

public static class DungeonDifficultyUtility
{
    public static void Inherit(EntityManager manager, Entity source, Entity target)
    {
        if (source == Entity.Null || !manager.Exists(source) ||
            !manager.HasComponent<DungeonDifficultyComponent>(source) ||
            manager.HasComponent<DungeonDifficultyComponent>(target))
            return;
        DungeonDifficultyComponent difficulty = manager.GetComponentData<DungeonDifficultyComponent>(source);
        difficulty.HealthApplied = 0;
        manager.AddComponentData(target, difficulty);
    }

    // Applied before saved current health is restored. No changes to shared UnitData or buffs.
    public static bool ApplyHealth(EntityManager manager, Entity entity)
    {
        if (!manager.HasComponent<DungeonDifficultyComponent>(entity) ||
            !manager.HasComponent<UnitVitalityComponent>(entity) ||
            !manager.HasComponent<UnitFactionComponent>(entity))
            return false;
        UnitFactionType faction = manager.GetComponentData<UnitFactionComponent>(entity).Value;
        if (faction != UnitFactionType.Enemy && faction != UnitFactionType.Boss)
            return false;
        DungeonDifficultyComponent difficulty = manager.GetComponentData<DungeonDifficultyComponent>(entity);
        if (difficulty.HealthApplied != 0 || !math.isfinite(difficulty.HealthMultiplier) || difficulty.HealthMultiplier < 1f)
            return false;
        UnitVitalityComponent vitality = manager.GetComponentData<UnitVitalityComponent>(entity);
        vitality.BaseMaxHealth *= difficulty.HealthMultiplier;
        UnitModifierComponent modifiers = manager.HasComponent<UnitModifierComponent>(entity)
            ? manager.GetComponentData<UnitModifierComponent>(entity) : UnitModifierComponent.CreateIdentity();
        vitality.CurrentHealth = UnitModifierResolver.GetMaxHealth(in vitality, in modifiers);
        vitality.NetworkDirty = 1;
        difficulty.HealthApplied = 1;
        manager.SetComponentData(entity, vitality);
        manager.SetComponentData(entity, difficulty);
        return true;
    }
}
