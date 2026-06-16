using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

public static class UnitSkillModifierUtility
{
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

    public static SkillModifierSet CreateCastModifiers(
        EntityManager entityManager,
        Entity entity,
        SkillData skillData = null,
        SkillAdditionData skillAdditionData = null)
    {
        SkillModifierSet modifiers = new();

        if (TryGetRuntimeComponent(entityManager, entity, out UnitSkillModifierRuntimeComponent runtimeComponent))
            modifiers.Add(runtimeComponent.Modifiers);

        if (skillAdditionData != null)
            modifiers.Add(skillAdditionData.Modifiers);

        if (skillData != null &&
            SkillExecutionUtility.SupportsFollowupEffects(entityManager, entity) &&
            entityManager.HasComponent<UnitCastFollowupRuntimeComponent>(entity))
        {
            SkillFollowupContext context = new(entityManager, entity, skillData, null, skillAdditionData);
            UnitCastFollowupRuntimeComponent followupComponent = entityManager.GetComponentObject<UnitCastFollowupRuntimeComponent>(entity);
            System.Collections.Generic.List<SkillFollowupRuntime> followupEffects = followupComponent?.Followups;
            if (followupEffects == null)
                return modifiers;

            for (int i = 0; i < followupEffects.Count; i++)
                CrystalMagic.Game.Skill.SkillResolver.ApplyFollowupModifiers(ref modifiers, followupEffects[i], context);
        }

        return modifiers;
    }
}
