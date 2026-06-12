using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

public static class UnitBuffUtility
{
    public static void AddRuntimeBuff(
        EntityManager entityManager,
        Entity entity,
        int buffId,
        int sourceSkillId,
        int stackCount = 1)
    {
        if (buffId < 0 || sourceSkillId < 0 || entity == Entity.Null || !entityManager.Exists(entity))
            return;

        BuffData buffData = DataComponent.Instance?.Get<BuffData>(buffId);
        if (buffData == null)
            return;

        UnitBuffRuntimeComponent runtimeComponent = GetOrCreateRuntimeComponent(entityManager, entity);
        if (runtimeComponent == null)
            return;

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = 0; i < buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            // Cast-task temporary buffs intentionally omit origin info so they do not collide with regular applied buffs.
            if (entry.BuffId != buffId || entry.SourceSkillId != sourceSkillId || entry.HasOriginEntity)
                continue;

            entry.StackCount = math.max(1, stackCount);
            entry.RemainingTime = -1f;
            entry.InitializeFromDefinition(buffData);
            return;
        }

        UnitBuffRuntimeEntry newEntry = new()
        {
            BuffId = buffId,
            RemainingTime = -1f,
            StackCount = math.max(1, stackCount),
            HasOriginEntity = false,
            OriginEntity = Entity.Null,
            SourceSkillId = sourceSkillId,
        };
        newEntry.InitializeFromDefinition(buffData);
        buffs.Add(newEntry);
    }

    public static void RemoveRuntimeBuffsBySourceSkillId(EntityManager entityManager, Entity entity, int sourceSkillId)
    {
        if (sourceSkillId < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return;
        }

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (!buffs[i].HasOriginEntity && buffs[i].SourceSkillId == sourceSkillId)
                buffs.RemoveAt(i);
        }
    }

    public static float ApplyDamageTakenRuntimeBuffs(EntityManager entityManager, Entity entity, float damage)
    {
        if (damage <= 0f ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return damage;
        }

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        PropertyModifierSet modifiers = new();
        for (int i = 0; i < buffs.Count; i++)
            buffs[i].ContributePropertyModifiers(modifiers);

        float finalDamage = math.max(
            0f,
            damage * modifiers.GetFactor(PropertyModifierChannel.DamageTakenMultiplier) +
            modifiers.GetBonus(PropertyModifierChannel.DamageTakenMultiplier));
        return finalDamage;
    }

    public static UnitBuffRuntimeComponent GetOrCreateRuntimeComponent(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return null;

        if (entityManager.HasComponent<UnitBuffRuntimeComponent>(entity))
            return entityManager.GetComponentObject<UnitBuffRuntimeComponent>(entity);

        UnitBuffRuntimeComponent runtimeComponent = new();
        entityManager.AddComponentObject(entity, runtimeComponent);
        return runtimeComponent;
    }

    public static bool TryGetRuntimeComponent(EntityManager entityManager, Entity entity, out UnitBuffRuntimeComponent runtimeComponent)
    {
        runtimeComponent = null;
        if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<UnitBuffRuntimeComponent>(entity))
            return false;

        runtimeComponent = entityManager.GetComponentObject<UnitBuffRuntimeComponent>(entity);
        return runtimeComponent != null;
    }
}
