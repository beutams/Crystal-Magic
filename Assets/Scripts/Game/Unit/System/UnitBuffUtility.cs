using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
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
            RuntimePayloadId = -1,
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
            {
                RemoveEffectBuffPayload(entityManager, entity, buffer[i].RuntimePayloadId);
                buffer.RemoveAt(i);
            }
        }
    }

    public static int SetEffectBuffPayload(
        EntityManager entityManager,
        Entity entity,
        int payloadId,
        EffectData[] runtimeEffectChain,
        bool hasOriginEntity,
        Entity originEntity)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return -1;

        UnitBuffPayloadComponent payloadComponent = GetOrCreatePayloadComponent(entityManager, entity);
        if (payloadComponent == null)
            return -1;

        int entryIndex = FindPayloadIndex(payloadComponent, payloadId);
        if (entryIndex < 0)
        {
            payloadId = payloadComponent.NextPayloadId++;
            payloadComponent.Entries.Add(new UnitBuffPayloadEntry
            {
                PayloadId = payloadId,
                HasOriginEntity = hasOriginEntity,
                OriginEntity = hasOriginEntity ? originEntity : Entity.Null,
                RuntimeEffectChain = runtimeEffectChain ?? System.Array.Empty<EffectData>(),
            });
            return payloadId;
        }

        UnitBuffPayloadEntry entry = payloadComponent.Entries[entryIndex];
        entry.HasOriginEntity = hasOriginEntity;
        entry.OriginEntity = hasOriginEntity ? originEntity : Entity.Null;
        entry.RuntimeEffectChain = runtimeEffectChain ?? System.Array.Empty<EffectData>();
        payloadComponent.Entries[entryIndex] = entry;
        return payloadId;
    }

    public static bool TryGetEffectBuffPayload(
        EntityManager entityManager,
        Entity entity,
        int payloadId,
        out UnitBuffPayloadEntry payload)
    {
        payload = null;
        if (payloadId < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitBuffPayloadComponent>(entity))
        {
            return false;
        }

        UnitBuffPayloadComponent payloadComponent = entityManager.GetComponentObject<UnitBuffPayloadComponent>(entity);
        int entryIndex = FindPayloadIndex(payloadComponent, payloadId);
        if (entryIndex < 0)
            return false;

        payload = payloadComponent.Entries[entryIndex];
        return payload != null;
    }

    public static void RemoveEffectBuffPayload(EntityManager entityManager, Entity entity, int payloadId)
    {
        if (payloadId < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitBuffPayloadComponent>(entity))
        {
            return;
        }

        UnitBuffPayloadComponent payloadComponent = entityManager.GetComponentObject<UnitBuffPayloadComponent>(entity);
        int entryIndex = FindPayloadIndex(payloadComponent, payloadId);
        if (entryIndex >= 0)
            payloadComponent.Entries.RemoveAt(entryIndex);
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

        float finalDamage = math.max(
            0f,
            damage * modifiers.GetFactor(PropertyModifierChannel.DamageTakenMultiplier) +
            modifiers.GetBonus(PropertyModifierChannel.DamageTakenMultiplier));
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

    private static UnitBuffPayloadComponent GetOrCreatePayloadComponent(EntityManager entityManager, Entity entity)
    {
        if (entityManager.HasComponent<UnitBuffPayloadComponent>(entity))
            return entityManager.GetComponentObject<UnitBuffPayloadComponent>(entity);

        UnitBuffPayloadComponent payloadComponent = new();
        entityManager.AddComponentObject(entity, payloadComponent);
        return payloadComponent;
    }

    private static int FindPayloadIndex(UnitBuffPayloadComponent payloadComponent, int payloadId)
    {
        if (payloadComponent?.Entries == null)
            return -1;

        for (int i = 0; i < payloadComponent.Entries.Count; i++)
        {
            if (payloadComponent.Entries[i]?.PayloadId == payloadId)
                return i;
        }

        return -1;
    }
}
