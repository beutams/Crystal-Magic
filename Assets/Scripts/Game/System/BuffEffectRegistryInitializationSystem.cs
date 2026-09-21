using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitBuffSystem))]
public partial class BuffEffectRegistryInitializationSystem : SystemBase
{
    private BlobAssetReference<BuffEffectRegistryBlob> _registry;

    protected override void OnCreate()
    {
        DataTable<BuffData> table = DataComponent.Instance.GetTable<BuffData>();
        List<BuffData> buffs = new(table.GetAll());
        buffs.Sort(static (left, right) => left.Id.CompareTo(right.Id));

        ModifierConfig modifierConfig = ConfigComponent.Instance.Get<ModifierConfig>();
        List<PropertyModifierRuntimeEntry> propertyModifiers = new();
        List<SkillModifierRuntimeEntry> skillModifiers = new();
        List<BuffTriggeredEffectBlob> triggeredEffects = new();
        List<List<BuffEffectReference>> referencesByBuff = new(buffs.Count);

        for (int buffIndex = 0; buffIndex < buffs.Count; buffIndex++)
        {
            BuffData buff = buffs[buffIndex];
            List<BuffEffectReference> references = new();

            if (buff.PropertyModifiers != null)
            {
                for (int modifierIndex = 0; modifierIndex < buff.PropertyModifiers.Count; modifierIndex++)
                {
                    int id = propertyModifiers.Count;
                    PropertyModifierEntry modifier = buff.PropertyModifiers[modifierIndex];
                    propertyModifiers.Add(new PropertyModifierRuntimeEntry
                    {
                        Value = modifier,
                        MinimumFactor = modifierConfig?.GetPropertyModifierMinimumFactor(modifier.Channel) ?? 0f,
                    });
                    references.Add(new BuffEffectReference { Type = BuffEffectType.PropertyModifier, Id = id });
                }
            }

            if (buff.SkillModifiers != null)
            {
                for (int modifierIndex = 0; modifierIndex < buff.SkillModifiers.Count; modifierIndex++)
                {
                    int id = skillModifiers.Count;
                    SkillModifierEntry modifier = buff.SkillModifiers[modifierIndex];
                    skillModifiers.Add(new SkillModifierRuntimeEntry
                    {
                        Value = modifier,
                        MinimumFactor = modifierConfig?.GetSkillModifierMinimumFactor(modifier.Channel) ?? 0f,
                    });
                    references.Add(new BuffEffectReference { Type = BuffEffectType.SkillModifier, Id = id });
                }
            }

            List<BuffTriggerEntry> triggers = buff.CreateEffectiveTriggerEntries();
            for (int triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                BuffTriggerEntry trigger = triggers[triggerIndex];
                if (trigger == null)
                    continue;

                EffectDataListId effectListId = EffectDataBridgeUtility.Register(EntityManager, trigger.Effects);
                int id = triggeredEffects.Count;
                triggeredEffects.Add(new BuffTriggeredEffectBlob
                {
                    TriggerType = trigger.TriggerType,
                    HookType = trigger.HookType,
                    TickIntervalSeconds = math.max(0f, trigger.TickIntervalSeconds),
                    ConsumeStackOnTrigger = trigger.ConsumeStackOnTrigger ? (byte)1 : (byte)0,
                    EffectListId = effectListId,
                });
                references.Add(new BuffEffectReference { Type = BuffEffectType.TriggeredEffect, Id = id });
            }

            referencesByBuff.Add(references);
        }

        using BlobBuilder builder = new(Allocator.Temp);
        ref BuffEffectRegistryBlob root = ref builder.ConstructRoot<BuffEffectRegistryBlob>();

        BlobBuilderArray<PropertyModifierRuntimeEntry> propertyArray =
            builder.Allocate(ref root.PropertyModifiers, propertyModifiers.Count);
        for (int i = 0; i < propertyModifiers.Count; i++)
            propertyArray[i] = propertyModifiers[i];

        BlobBuilderArray<SkillModifierRuntimeEntry> skillArray =
            builder.Allocate(ref root.SkillModifiers, skillModifiers.Count);
        for (int i = 0; i < skillModifiers.Count; i++)
            skillArray[i] = skillModifiers[i];

        BlobBuilderArray<BuffTriggeredEffectBlob> triggerArray =
            builder.Allocate(ref root.TriggeredEffects, triggeredEffects.Count);
        for (int i = 0; i < triggeredEffects.Count; i++)
            triggerArray[i] = triggeredEffects[i];

        BlobBuilderArray<BuffDefinitionBlob> buffArray = builder.Allocate(ref root.Buffs, buffs.Count);
        for (int buffIndex = 0; buffIndex < buffs.Count; buffIndex++)
        {
            BuffData source = buffs[buffIndex];
            ref BuffDefinitionBlob definition = ref buffArray[buffIndex];
            definition.BuffId = source.Id;
            definition.CanStack = source.CanStack ? (byte)1 : (byte)0;
            definition.MaxStacks = math.max(1, source.MaxStacks);

            List<BuffEffectReference> references = referencesByBuff[buffIndex];
            BlobBuilderArray<BuffEffectReference> referenceArray =
                builder.Allocate(ref definition.Effects, references.Count);
            for (int referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
                referenceArray[referenceIndex] = references[referenceIndex];
        }

        _registry = builder.CreateBlobAssetReference<BuffEffectRegistryBlob>(Allocator.Persistent);
        Entity entity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(entity, new BuffEffectRegistryComponent { Value = _registry });
        Enabled = false;
    }

    protected override void OnUpdate()
    {
    }

    protected override void OnDestroy()
    {
        if (!_registry.IsCreated)
            return;

        ref BuffEffectRegistryBlob registry = ref _registry.Value;
        for (int i = 0; i < registry.TriggeredEffects.Length; i++)
            EffectDataBridgeUtility.Unregister(EntityManager, registry.TriggeredEffects[i].EffectListId);

        _registry.Dispose();
    }
}
