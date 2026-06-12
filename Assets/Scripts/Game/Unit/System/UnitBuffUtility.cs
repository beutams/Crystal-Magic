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
        int executionToken,
        int stackCount = 1,
        bool consumeOnDamageTaken = false,
        int remainingTriggerCount = 1)
    {
        if (buffId < 0 || entity == Entity.Null || !entityManager.Exists(entity))
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
            if (entry.BuffId != buffId || entry.SourceExecutionToken != executionToken)
                continue;

            entry.StackCount = math.max(1, stackCount);
            entry.ConsumeOnDamageTaken = consumeOnDamageTaken;
            entry.RemainingTriggerCount = consumeOnDamageTaken ? math.max(1, remainingTriggerCount) : 0;
            entry.RemainingTime = -1f;
            entry.NextTickTime = 0f;
            entry.InitializeFromDefinition(buffData, entry.RuntimeEffectChain);
            return;
        }

        UnitBuffRuntimeEntry newEntry = new()
        {
            BuffId = buffId,
            RemainingTime = -1f,
            NextTickTime = 0f,
            StackCount = math.max(1, stackCount),
            HasOriginEntity = false,
            OriginEntity = Entity.Null,
            SourceSkillId = -1,
            SourceExecutionToken = executionToken,
            ConsumeOnDamageTaken = consumeOnDamageTaken,
            RemainingTriggerCount = consumeOnDamageTaken ? math.max(1, remainingTriggerCount) : 0,
        };
        newEntry.InitializeFromDefinition(buffData);
        buffs.Add(newEntry);
    }

    public static void RemoveRuntimeBuffsByExecutionToken(EntityManager entityManager, Entity entity, int executionToken)
    {
        if (executionToken < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return;
        }

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (buffs[i].SourceExecutionToken == executionToken)
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
        if (finalDamage < damage)
            ConsumeDamageTakenRuntimeBuffs(buffs);

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

    private static void ConsumeDamageTakenRuntimeBuffs(List<UnitBuffRuntimeEntry> buffs)
    {
        if (buffs == null)
            return;

        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            if (entry.SourceExecutionToken < 0 || !entry.ConsumeOnDamageTaken)
                continue;

            entry.RemainingTriggerCount--;
            if (entry.RemainingTriggerCount <= 0)
                buffs.RemoveAt(i);
        }
    }
}
