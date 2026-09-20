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
        int sourceSkillId)
    {
        if (buffId < 0 || entity == Entity.Null || !entityManager.Exists(entity) ||
            !BuffEffectRegistryUtility.TryGet(entityManager, out BlobAssetReference<BuffEffectRegistryBlob> registry))
        {
            return false;
        }

        int definitionIndex = BuffEffectRegistryUtility.FindBuffIndex(in registry, buffId);
        if (definitionIndex < 0)
            return false;

        DynamicBuffer<UnitBuffElement> buffs = GetOrCreateRuntimeBuffer(entityManager, entity);
        ref BuffDefinitionBlob definition = ref registry.Value.Buffs[definitionIndex];
        int stackToApply = math.max(1, stackCount);
        float duration = durationSeconds < 0f ? -1f : math.max(0f, durationSeconds);

        for (int i = 0; i < buffs.Length; i++)
        {
            UnitBuffElement entry = buffs[i];
            if (entry.BuffId != buffId)
                continue;

            entry.DefinitionIndex = definitionIndex;
            entry.RemainingTime = GetPreferredDuration(entry.RemainingTime, duration);
            entry.ElapsedTime = 0f;
            entry.StackCount = definition.CanStack != 0
                ? math.min(math.max(1, definition.MaxStacks), math.max(1, entry.StackCount) + stackToApply)
                : 1;
            entry.OriginEntity = originEntity;
            entry.SourceSkillId = sourceSkillId;
            buffs[i] = entry;
            MarkDirty(entityManager, entity);
            return true;
        }

        buffs.Add(new UnitBuffElement
        {
            BuffId = buffId,
            DefinitionIndex = definitionIndex,
            RemainingTime = duration,
            ElapsedTime = 0f,
            StackCount = definition.CanStack != 0
                ? math.min(math.max(1, definition.MaxStacks), stackToApply)
                : 1,
            OriginEntity = originEntity,
            SourceSkillId = sourceSkillId,
        });
        MarkDirty(entityManager, entity);
        return true;
    }

    public static bool Remove(
        EntityManager entityManager,
        Entity entity,
        int buffId,
        bool removeAllStacks,
        int removeStackCount = 1)
    {
        if (buffId < 0 || !TryGetRuntimeBuffer(entityManager, entity, out DynamicBuffer<UnitBuffElement> buffs))
            return false;

        for (int i = 0; i < buffs.Length; i++)
        {
            UnitBuffElement entry = buffs[i];
            if (entry.BuffId != buffId)
                continue;

            if (removeAllStacks)
            {
                buffs.RemoveAt(i);
            }
            else
            {
                entry.StackCount -= math.max(1, removeStackCount);
                if (entry.StackCount <= 0)
                    buffs.RemoveAt(i);
                else
                    buffs[i] = entry;
            }

            MarkDirty(entityManager, entity);
            return true;
        }

        return false;
    }

    public static bool RemoveAll(EntityManager entityManager, Entity entity, int buffId)
    {
        if (buffId < 0 || !TryGetRuntimeBuffer(entityManager, entity, out DynamicBuffer<UnitBuffElement> buffs))
            return false;

        UnitBuffComponent component = entityManager.GetComponentData<UnitBuffComponent>(entity);
        bool removed = RemoveAll(ref component, buffs, buffId);
        if (removed)
            entityManager.SetComponentData(entity, component);

        return removed;
    }

    public static bool RemoveAll(
        ref UnitBuffComponent component,
        DynamicBuffer<UnitBuffElement> buffs,
        int buffId)
    {
        if (buffId < 0)
            return false;

        bool removed = false;
        for (int i = buffs.Length - 1; i >= 0; i--)
        {
            if (buffs[i].BuffId != buffId)
                continue;

            buffs.RemoveAt(i);
            removed = true;
        }

        if (removed)
            MarkDirty(ref component);

        return removed;
    }

    public static bool TryRemoveStacks(
        EntityManager entityManager,
        Entity entity,
        int buffId,
        int stackCount)
    {
        if (buffId < 0 || stackCount <= 0 ||
            !TryGetRuntimeBuffer(entityManager, entity, out DynamicBuffer<UnitBuffElement> buffs))
        {
            return false;
        }

        UnitBuffComponent component = entityManager.GetComponentData<UnitBuffComponent>(entity);
        bool removed = TryRemoveStacks(ref component, buffs, buffId, stackCount);
        if (removed)
            entityManager.SetComponentData(entity, component);

        return removed;
    }

    public static bool TryRemoveStacks(
        ref UnitBuffComponent component,
        DynamicBuffer<UnitBuffElement> buffs,
        int buffId,
        int stackCount)
    {
        if (buffId < 0 || stackCount <= 0)
            return false;

        for (int i = 0; i < buffs.Length; i++)
        {
            UnitBuffElement entry = buffs[i];
            if (entry.BuffId != buffId)
                continue;

            if (entry.StackCount < stackCount)
                return false;

            entry.StackCount -= stackCount;
            if (entry.StackCount == 0)
                buffs.RemoveAt(i);
            else
                buffs[i] = entry;

            MarkDirty(ref component);
            return true;
        }

        return false;
    }

    public static int GetStackCount(EntityManager entityManager, Entity entity, int buffId)
    {
        if (buffId < 0 || !TryGetRuntimeBuffer(entityManager, entity, out DynamicBuffer<UnitBuffElement> buffs))
            return 0;

        for (int i = 0; i < buffs.Length; i++)
        {
            UnitBuffElement entry = buffs[i];
            if (entry.BuffId == buffId)
                return math.max(0, entry.StackCount);
        }

        return 0;
    }

    public static DynamicBuffer<UnitBuffElement> GetOrCreateRuntimeBuffer(
        EntityManager entityManager,
        Entity entity)
    {
        if (!entityManager.HasComponent<UnitBuffComponent>(entity))
            entityManager.AddComponentData(entity, new UnitBuffComponent { ModifierDirty = 1 });
        if (!entityManager.HasComponent<UnitModifierComponent>(entity))
            entityManager.AddComponentData(entity, UnitModifierComponent.CreateIdentity());

        DynamicBuffer<UnitBuffElement> buffs = entityManager.HasBuffer<UnitBuffElement>(entity)
            ? entityManager.GetBuffer<UnitBuffElement>(entity)
            : entityManager.AddBuffer<UnitBuffElement>(entity);

        if (!entityManager.HasBuffer<UnitBuffHookRequestElement>(entity))
            entityManager.AddBuffer<UnitBuffHookRequestElement>(entity);
        if (!entityManager.HasBuffer<EffectEntry>(entity))
            entityManager.AddBuffer<EffectEntry>(entity);

        return buffs;
    }

    public static bool TryGetRuntimeBuffer(
        EntityManager entityManager,
        Entity entity,
        out DynamicBuffer<UnitBuffElement> buffs)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitBuffComponent>(entity) ||
            !entityManager.HasBuffer<UnitBuffElement>(entity))
        {
            buffs = default;
            return false;
        }

        buffs = entityManager.GetBuffer<UnitBuffElement>(entity);
        return true;
    }

    private static void MarkDirty(EntityManager entityManager, Entity entity)
    {
        UnitBuffComponent component = entityManager.GetComponentData<UnitBuffComponent>(entity);
        MarkDirty(ref component);
        entityManager.SetComponentData(entity, component);
    }

    private static void MarkDirty(ref UnitBuffComponent component)
    {
        component.NetworkDirty = 1;
        component.ModifierDirty = 1;
    }

    private static float GetPreferredDuration(float currentDuration, float incomingDuration)
    {
        if (currentDuration < 0f || incomingDuration < 0f)
            return -1f;

        return math.max(currentDuration, incomingDuration);
    }
}
