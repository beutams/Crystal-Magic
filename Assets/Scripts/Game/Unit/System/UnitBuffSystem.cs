using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitRecoverySystem))]
partial class UnitBuffSystem : SystemBase
{
    private EntityQuery _buffRuntimeQuery;
    private readonly BuffUpdateContext _updateContext = new();

    protected override void OnCreate()
    {
        _buffRuntimeQuery = GetEntityQuery(ComponentType.ReadOnly<UnitBuffRuntimeComponent>());
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        PendingEffectExecutionQueueComponent effectExecutionQueue = PendingEffectExecutionQueueUtility.GetOrCreate(EntityManager);
        using NativeArray<Entity> buffEntities = _buffRuntimeQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < buffEntities.Length; entityIndex++)
        {
            Entity entity = buffEntities[entityIndex];
            if (!UnitBuffUtility.TryGetRuntimeComponent(EntityManager, entity, out UnitBuffRuntimeComponent runtimeComponent))
                continue;

            UpdateBuffEntries(entity, runtimeComponent, effectExecutionQueue, dt);
            PropertyModifierSet modifiers = BuildPropertyModifiers(runtimeComponent.Buffs);
            SkillModifierSet skillModifiers = BuildSkillModifiers(runtimeComponent.Buffs);
            UnitModifierUtility.ApplyRuntimePropertyModifiers(EntityManager, entity, modifiers);
            UnitSkillModifierUtility.AddRuntimeModifiers(EntityManager, entity, skillModifiers);
        }
    }

    private void UpdateBuffEntries(
        Entity entity,
        UnitBuffRuntimeComponent runtimeComponent,
        PendingEffectExecutionQueueComponent effectExecutionQueue,
        float deltaTime)
    {
        List<UnitBuffRuntimeEntry> buffs = runtimeComponent.Buffs;
        _updateContext.EntityManager = EntityManager;
        _updateContext.TargetEntity = entity;
        _updateContext.EffectExecutionQueue = effectExecutionQueue;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            UnitBuffRuntimeEntry entry = buffs[i];
            if (!entry.Update(_updateContext, deltaTime))
                buffs.RemoveAt(i);
        }
    }

    private static PropertyModifierSet BuildPropertyModifiers(List<UnitBuffRuntimeEntry> buffs)
    {
        PropertyModifierSet modifiers = new();
        if (buffs == null)
            return modifiers;

        for (int i = 0; i < buffs.Count; i++)
            buffs[i].ContributePropertyModifiers(modifiers);

        return modifiers;
    }

    private static SkillModifierSet BuildSkillModifiers(List<UnitBuffRuntimeEntry> buffs)
    {
        SkillModifierSet modifiers = new();
        if (buffs == null)
            return modifiers;

        for (int i = 0; i < buffs.Count; i++)
            buffs[i].ContributeSkillModifiers(modifiers);

        return modifiers;
    }
}
