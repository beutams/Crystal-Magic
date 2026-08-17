using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

public static class UnitSkillModifierUtility
{
    public static int GetModifiedMpCost(SkillModifierSet modifiers, float baseMpCost)
    {
        if (!math.isfinite(baseMpCost))
            return 0;

        float modifiedMpCost = modifiers?.Apply(SkillModifierChannel.MpCost, baseMpCost) ?? baseMpCost;
        return math.max(0, (int)math.round(modifiedMpCost));
    }

    public static UnitSkillModifierRuntimeComponent GetOrCreateRuntimeComponent(EntityManager entityManager, Entity entity)
    {
        if (entityManager.HasComponent<UnitSkillModifierRuntimeComponent>(entity))
            return entityManager.GetComponentObject<UnitSkillModifierRuntimeComponent>(entity);

        UnitSkillModifierRuntimeComponent component = new();
        entityManager.AddComponentObject(entity, component);
        return component;
    }

    public static bool TryGetRuntimeComponent(
        EntityManager entityManager,
        Entity entity,
        out UnitSkillModifierRuntimeComponent runtimeComponent)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<UnitSkillModifierRuntimeComponent>(entity))
        {
            runtimeComponent = null;
            return false;
        }

        runtimeComponent = entityManager.GetComponentObject<UnitSkillModifierRuntimeComponent>(entity);
        return runtimeComponent != null;
    }

    public static void ResetRuntimeModifiers(EntityManager entityManager, Entity entity)
    {
        UnitSkillModifierRuntimeComponent runtimeComponent = GetOrCreateRuntimeComponent(entityManager, entity);
        runtimeComponent.Modifiers = new SkillModifierSet();
    }

    public static void AddRuntimeModifiers(EntityManager entityManager, Entity entity, SkillModifierSet modifiers)
    {
        if (modifiers == null)
            return;

        UnitSkillModifierRuntimeComponent runtimeComponent = GetOrCreateRuntimeComponent(entityManager, entity);
        runtimeComponent.Modifiers ??= new SkillModifierSet();
        runtimeComponent.Modifiers.Add(modifiers);
    }

    public static void AddRuntimeModifiers(EntityManager entityManager, Entity entity, SkillModifierEntry entry, int stacks = 1)
    {
        UnitSkillModifierRuntimeComponent runtimeComponent = GetOrCreateRuntimeComponent(entityManager, entity);
        runtimeComponent.Modifiers ??= new SkillModifierSet();
        runtimeComponent.Modifiers.Add(entry, stacks);
    }

    public static SkillModifierSet CreateSnapshot(EntityManager entityManager, Entity entity)
    {
        SkillModifierSet modifiers = new();

        if (TryGetRuntimeComponent(entityManager, entity, out UnitSkillModifierRuntimeComponent runtimeComponent))
            modifiers.Add(runtimeComponent.Modifiers);

        return modifiers;
    }
}
