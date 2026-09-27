using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using CrystalMagic.Core;
using System.Collections.Generic;
using Unity.Entities;

public static class EffectDataBridgeUtility
{
    private static readonly IObjectPool<EffectDataList> EffectDataLists =
        new ObjectPool<EffectDataList>(
            static () => new EffectDataList(),
            initialSize: 32,
            maxSize: 4096);

    private static readonly IObjectPool<EffectManagedContextState> ManagedContexts =
        new ObjectPool<EffectManagedContextState>(
            static () => new EffectManagedContextState(),
            initialSize: 16,
            maxSize: 4096);

    private static readonly IObjectPool<List<ConditionConfig>> ConditionLists =
        new ObjectPool<List<ConditionConfig>>(
            static () => new List<ConditionConfig>(),
            initialSize: 16,
            maxSize: 4096,
            onReturn: static conditions => conditions.Clear());

    public static EffectDataBridgeComponent GetOrCreate(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EffectDataBridgeComponent>());
        if (!query.IsEmptyIgnoreFilter)
            return entityManager.GetComponentObject<EffectDataBridgeComponent>(query.GetSingletonEntity());

        Entity entity = entityManager.CreateEntity();
        EffectDataBridgeComponent bridge = new();
        entityManager.AddComponentObject(entity, bridge);
        return bridge;
    }

    public static EffectDataListId Register(EntityManager entityManager, EffectData[] effects, bool registry = false)
    {
        if (effects == null || effects.Length == 0)
            return default;

        EffectDataListId id = EffectDataListIdAllocator.Allocate();
        EffectDataBridgeComponent bridge = GetOrCreate(entityManager);
        EffectDataList effectDataList = EffectDataLists.Get();
        effectDataList.Initialize(effects);
        bridge.Values.Add(id.Value, effectDataList);
        if (registry)
            bridge.RegistryIds.Add(id.Value);
        return id;
    }

    public static bool TryGet(
        EntityManager entityManager,
        EffectDataListId id,
        out EffectDataList effectDataList)
    {
        effectDataList = null;
        return id.IsValid && GetOrCreate(entityManager).Values.TryGetValue(id.Value, out effectDataList);
    }

    public static void Unregister(EntityManager entityManager, EffectDataListId id)
    {
        if (id.IsValid)
        {
            EffectDataBridgeComponent bridge = GetOrCreate(entityManager);
            if (bridge.Values.TryGetValue(id.Value, out EffectDataList effectDataList))
            {
                bridge.Values.Remove(id.Value);
                if (effectDataList.IsPoolOwned)
                    EffectDataLists.Return(effectDataList);
            }
            bridge.RegistryIds.Remove(id.Value);
        }
    }

    public static EffectManagedContextId RegisterManagedContext(
        EntityManager entityManager,
        SkillContent context)
    {
        if (context == null ||
            (!context.HasTarget &&
             context.Target == null &&
             context.Origin == null &&
             context.PersistentEffectAppliedBuffTargets == null))
        {
            return default;
        }

        EffectManagedContextId id = EffectManagedContextIdAllocator.Allocate();
        EffectManagedContextState state = ManagedContexts.Get();
        state.HasTarget = context.HasTarget;
        state.Target = context.Target;
        state.Origin = context.Origin;
        state.PersistentEffectAppliedBuffTargets = context.PersistentEffectAppliedBuffTargets;
        GetOrCreate(entityManager).ManagedContexts.Add(id.Value, state);
        return id;
    }

    public static bool TryGetManagedContext(
        EntityManager entityManager,
        EffectManagedContextId id,
        out EffectManagedContextState state)
    {
        state = null;
        return id.IsValid && GetOrCreate(entityManager).ManagedContexts.TryGetValue(id.Value, out state);
    }

    public static void UnregisterManagedContext(EntityManager entityManager, EffectManagedContextId id)
    {
        if (!id.IsValid)
            return;

        EffectDataBridgeComponent bridge = GetOrCreate(entityManager);
        if (!bridge.ManagedContexts.TryGetValue(id.Value, out EffectManagedContextState state))
            return;

        bridge.ManagedContexts.Remove(id.Value);
        ManagedContexts.Return(state);
    }

    public static ConditionDataListId RegisterConditions(
        EntityManager entityManager,
        IReadOnlyList<ConditionConfig> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return default;

        ConditionDataListId id = ConditionDataListIdAllocator.Allocate();
        List<ConditionConfig> copy = ConditionLists.Get();
        if (copy.Capacity < conditions.Count)
            copy.Capacity = conditions.Count;
        for (int i = 0; i < conditions.Count; i++)
            copy.Add(conditions[i]);
        GetOrCreate(entityManager).ConditionLists.Add(id.Value, copy);
        return id;
    }

    public static bool TryGetConditions(
        EntityManager entityManager,
        ConditionDataListId id,
        out List<ConditionConfig> conditions)
    {
        conditions = null;
        return id.IsValid && GetOrCreate(entityManager).ConditionLists.TryGetValue(id.Value, out conditions);
    }

    public static void UnregisterConditions(EntityManager entityManager, ConditionDataListId id)
    {
        if (!id.IsValid)
            return;

        EffectDataBridgeComponent bridge = GetOrCreate(entityManager);
        if (!bridge.ConditionLists.TryGetValue(id.Value, out List<ConditionConfig> conditions))
            return;

        bridge.ConditionLists.Remove(id.Value);
        ConditionLists.Return(conditions);
    }

    public static void ClearTransient(EntityManager entityManager)
    {
        EffectDataBridgeComponent bridge = GetOrCreate(entityManager);
        List<int> transientEffectIds = new(bridge.Values.Count);
        foreach (int id in bridge.Values.Keys)
        {
            if (!bridge.RegistryIds.Contains(id))
                transientEffectIds.Add(id);
        }

        for (int index = 0; index < transientEffectIds.Count; index++)
            Unregister(entityManager, new EffectDataListId(transientEffectIds[index]));

        foreach (EffectManagedContextState state in bridge.ManagedContexts.Values)
            ManagedContexts.Return(state);
        bridge.ManagedContexts.Clear();

        foreach (List<ConditionConfig> conditions in bridge.ConditionLists.Values)
            ConditionLists.Return(conditions);
        bridge.ConditionLists.Clear();
    }
}
