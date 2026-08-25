using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

public static class UnitBuffUtility
{
    public static bool Apply(
        EntityManager entityManager,
        Entity entity,
        int buffId,
        float durationSeconds,
        int stackCount,
        Entity originEntity,
        int sourceSkillId,
        List<BuffTriggerRuntimeEntry> runtimeTriggerEntries = null)
    {
        if (buffId < 0 || entity == Entity.Null || !entityManager.Exists(entity))
            return false;

        BuffData buffData = DataComponent.Instance?.Get<BuffData>(buffId);
        if (buffData == null)
            return false;

        UnitBuffRuntimeComponent runtimeComponent = GetOrCreateRuntimeComponent(entityManager, entity);
        if (runtimeComponent == null)
            return false;

        int stackToApply = math.max(1, stackCount);
        float duration = durationSeconds < 0f ? -1f : math.max(0f, durationSeconds);
        bool hasOriginEntity = originEntity != Entity.Null;

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = 0; i < buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            if (entry == null || entry.BuffId != buffId)
                continue;

            entry.RemainingTime = GetPreferredDuration(entry.RemainingTime, duration);
            entry.StackCount = buffData.CanStack
                ? math.min(math.max(1, buffData.MaxStacks), math.max(1, entry.StackCount) + stackToApply)
                : 1;
            entry.HasOriginEntity = hasOriginEntity;
            entry.OriginEntity = hasOriginEntity ? originEntity : Entity.Null;
            entry.SourceSkillId = sourceSkillId;
            entry.InitializeFromDefinition(buffData, runtimeTriggerEntries);
            return true;
        }

        UnitBuffRuntimeEntry newEntry = new()
        {
            BuffId = buffId,
            RemainingTime = duration,
            StackCount = buffData.CanStack
                ? math.min(math.max(1, buffData.MaxStacks), stackToApply)
                : 1,
            HasOriginEntity = hasOriginEntity,
            OriginEntity = hasOriginEntity ? originEntity : Entity.Null,
            SourceSkillId = sourceSkillId,
        };
        newEntry.InitializeFromDefinition(buffData, runtimeTriggerEntries);
        buffs.Add(newEntry);
        return true;
    }

    public static bool Remove(EntityManager entityManager, Entity entity, int buffId, bool removeAllStacks, int removeStackCount = 1)
    {
        if (buffId < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return false;
        }

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = 0; i < buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            if (entry == null || entry.BuffId != buffId)
                continue;

            if (removeAllStacks)
            {
                buffs.RemoveAt(i);
                return true;
            }

            entry.StackCount -= math.max(1, removeStackCount);
            if (entry.StackCount <= 0)
                buffs.RemoveAt(i);

            return true;
        }

        return false;
    }

    public static bool RemoveAll(EntityManager entityManager, Entity entity, int buffId)
    {
        if (buffId < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return false;
        }

        bool removed = false;
        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (buffs[i]?.BuffId != buffId)
                continue;

            buffs.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    public static bool TryRemoveStacks(EntityManager entityManager, Entity entity, int buffId, int stackCount)
    {
        if (buffId < 0 ||
            stackCount <= 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return false;
        }

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = 0; i < buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            if (entry?.BuffId != buffId)
                continue;

            if (entry.StackCount < stackCount)
                return false;

            entry.StackCount -= stackCount;
            if (entry.StackCount == 0)
                buffs.RemoveAt(i);

            return true;
        }

        return false;
    }

    public static int GetStackCount(EntityManager entityManager, Entity entity, int buffId)
    {
        if (buffId < 0 ||
            entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
        {
            return 0;
        }

        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        for (int i = 0; i < buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            if (entry?.BuffId == buffId)
                return math.max(0, entry.StackCount);
        }

        return 0;
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

    private static float GetPreferredDuration(float currentDuration, float incomingDuration)
    {
        if (currentDuration < 0f || incomingDuration < 0f)
            return -1f;

        return math.max(currentDuration, incomingDuration);
    }
}
