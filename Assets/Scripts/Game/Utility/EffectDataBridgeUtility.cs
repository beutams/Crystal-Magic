using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using System.Collections.Generic;
using Unity.Entities;

public static class EffectDataBridgeUtility
{
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

    public static EffectDataListId Register(EntityManager entityManager, EffectData[] effects)
    {
        if (effects == null || effects.Length == 0)
            return default;

        EffectDataListId id = EffectDataListIdAllocator.Allocate();
        GetOrCreate(entityManager).Values.Add(id.Value, new EffectDataList(effects));
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
            GetOrCreate(entityManager).Values.Remove(id.Value);
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
        GetOrCreate(entityManager).ManagedContexts.Add(id.Value, new EffectManagedContextState
        {
            HasTarget = context.HasTarget,
            Target = context.Target,
            Origin = context.Origin,
            PersistentEffectAppliedBuffTargets = context.PersistentEffectAppliedBuffTargets,
        });
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
        if (id.IsValid)
            GetOrCreate(entityManager).ManagedContexts.Remove(id.Value);
    }

    public static ConditionDataListId RegisterConditions(
        EntityManager entityManager,
        IReadOnlyList<ConditionConfig> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return default;

        ConditionDataListId id = ConditionDataListIdAllocator.Allocate();
        List<ConditionConfig> copy = new(conditions.Count);
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
        if (id.IsValid)
            GetOrCreate(entityManager).ConditionLists.Remove(id.Value);
    }
}
