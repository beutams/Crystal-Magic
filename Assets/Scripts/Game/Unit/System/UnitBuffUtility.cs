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

        if (!entityManager.HasBuffer<UnitBuffElement>(entity))
            entityManager.AddBuffer<UnitBuffElement>(entity);

        DynamicBuffer<UnitBuffElement> buffer = entityManager.GetBuffer<UnitBuffElement>(entity);
        for (int i = 0; i < buffer.Length; i++)
        {
            UnitBuffElement element = buffer[i];
            if (element.BuffId != buffId || element.SourceExecutionToken != executionToken)
                continue;

            element.StackCount = math.max(1, stackCount);
            element.ConsumeOnDamageTaken = consumeOnDamageTaken ? (byte)1 : (byte)0;
            element.RemainingTriggerCount = consumeOnDamageTaken ? math.max(1, remainingTriggerCount) : 0;
            element.RemainingTime = -1f;
            element.NextTickTime = 0f;
            buffer[i] = element;
            return;
        }

        buffer.Add(new UnitBuffElement
        {
            BuffId = buffId,
            RemainingTime = -1f,
            NextTickTime = 0f,
            StackCount = math.max(1, stackCount),
            SourceExecutionToken = executionToken,
            ConsumeOnDamageTaken = consumeOnDamageTaken ? (byte)1 : (byte)0,
            RemainingTriggerCount = consumeOnDamageTaken ? math.max(1, remainingTriggerCount) : 0,
        });
    }

    public static void RemoveRuntimeBuffsByExecutionToken(EntityManager entityManager, Entity entity, int executionToken)
    {
        if (executionToken < 0 || entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasBuffer<UnitBuffElement>(entity))
            return;

        DynamicBuffer<UnitBuffElement> buffer = entityManager.GetBuffer<UnitBuffElement>(entity);
        for (int i = buffer.Length - 1; i >= 0; i--)
        {
            if (buffer[i].SourceExecutionToken == executionToken)
                buffer.RemoveAt(i);
        }
    }

    public static float ApplyDamageTakenRuntimeBuffs(EntityManager entityManager, Entity entity, float damage)
    {
        if (damage <= 0f || entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasBuffer<UnitBuffElement>(entity))
            return damage;

        DynamicBuffer<UnitBuffElement> buffer = entityManager.GetBuffer<UnitBuffElement>(entity);
        PropertyModifierSet modifiers = new();
        for (int i = 0; i < buffer.Length; i++)
        {
            UnitBuffElement element = buffer[i];
            if (DataComponent.Instance?.Get<BuffData>(element.BuffId) is not PropertyBuffData propertyBuffData)
                continue;

            modifiers.Add(propertyBuffData.PropertyModifiers, element.StackCount > 0 ? element.StackCount : 1);
        }

        float finalDamage = math.max(0f, damage * math.max(0f, modifiers.GetFactor(PropertyModifierChannel.DamageTakenMultiplier) + modifiers.GetBonus(PropertyModifierChannel.DamageTakenMultiplier)));
        if (finalDamage < damage)
            ConsumeDamageTakenRuntimeBuffs(buffer);

        return finalDamage;
    }

    private static void ConsumeDamageTakenRuntimeBuffs(DynamicBuffer<UnitBuffElement> buffer)
    {
        for (int i = buffer.Length - 1; i >= 0; i--)
        {
            UnitBuffElement element = buffer[i];
            if (element.SourceExecutionToken < 0 || element.ConsumeOnDamageTaken == 0)
                continue;

            element.RemainingTriggerCount--;
            if (element.RemainingTriggerCount <= 0)
                buffer.RemoveAt(i);
            else
                buffer[i] = element;
        }
    }
}
