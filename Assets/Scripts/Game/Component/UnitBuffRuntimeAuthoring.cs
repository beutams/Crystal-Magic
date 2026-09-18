using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

public class UnitBuffRuntimeAuthoring : MonoBehaviour
{
    class UnitBuffRuntimeBaker : Baker<UnitBuffRuntimeAuthoring>
    {
        public override void Bake(UnitBuffRuntimeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            UnitBuffRuntimeComponent runtimeComponent = new();
            AddComponentObject(entity, runtimeComponent);
        }
    }
}

public class UnitBuffRuntimeComponent : IComponentData
{
    public List<UnitBuffRuntimeEntry> Buffs = new();
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(UnitBuffRuntimeComponent), typeof(UnitBuffRuntimeAuthoring))]
public static class UnitBuffSource
{
    [UnitSourceGet(0, "unit.buffs.count", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.buffs.idAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(2, "unit.buffs.remainingTimeAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(3, "unit.buffs.stackCountAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(4, "unit.buffs.hasOriginAt", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(5, "unit.buffs.originEntityAt", UnitValueCategory.Entity, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(6, "unit.buffs.sourceSkillIdAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(7, "unit.buffs.findIndex", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    [UnitSourceGet(8, "unit.buffs.has", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    [UnitSourceGet(9, "unit.buffs.stackCount", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!TryGetComponent(entityManager, entity, out UnitBuffRuntimeComponent component))
            return false;

        if (operation == 0)
        {
            result = UnitSourceValue.FromInt(component.Buffs?.Count ?? 0);
            return true;
        }

        if (operation >= 1 && operation <= 6)
        {
            if (!GetEntry(component, in arguments, out UnitBuffRuntimeEntry entry))
                return false;

            result = operation switch
            {
                1 => UnitSourceValue.FromInt(entry.BuffId),
                2 => UnitSourceValue.FromFloat(entry.RemainingTime),
                3 => UnitSourceValue.FromInt(entry.StackCount),
                4 => UnitSourceValue.FromBool(entry.HasOriginEntity),
                5 => UnitSourceValue.FromEntity(entry.OriginEntity),
                6 => UnitSourceValue.FromInt(entry.SourceSkillId),
                _ => UnitSourceValue.None,
            };
            return true;
        }

        int index = FindIndex(component, in arguments);
        switch (operation)
        {
            case 7:
                result = UnitSourceValue.FromInt(index);
                return true;
            case 8:
                result = UnitSourceValue.FromBool(index >= 0);
                return true;
            case 9:
                result = UnitSourceValue.FromInt(index >= 0 ? component.Buffs[index].StackCount : 0);
                return true;
            default:
                return false;
        }
    }

    [UnitSourceSet(0, "unit.buffs.remove", UnitValueCategory.Number, ParameterNames = new[] { "BuffId" })]
    [UnitSourceSet(1, "unit.buffs.removeStacks", UnitValueCategory.Number, UnitValueCategory.Number,
        ParameterNames = new[] { "BuffId", "StackCount" })]
    public static bool TrySet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments)
    {
        if (!arguments.TryGetInt(0, out int buffId))
            return false;

        if (operation == 0)
        {
            UnitBuffUtility.RemoveAll(entityManager, entity, buffId);
            return true;
        }

        return operation == 1 && arguments.TryGetInt(1, out int stackCount) &&
               UnitBuffUtility.TryRemoveStacks(entityManager, entity, buffId, stackCount);
    }

    private static bool TryGetComponent(
        EntityManager entityManager,
        Entity entity,
        out UnitBuffRuntimeComponent component)
    {
        component = null;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitBuffRuntimeComponent>(entity))
            return false;

        component = entityManager.GetComponentObject<UnitBuffRuntimeComponent>(entity);
        return component != null;
    }

    private static bool GetEntry(
        UnitBuffRuntimeComponent component,
        in UnitSourceArguments arguments,
        out UnitBuffRuntimeEntry entry)
    {
        entry = null;
        if (!arguments.TryGetInt(0, out int index) || component?.Buffs == null ||
            index < 0 || index >= component.Buffs.Count)
        {
            return false;
        }

        entry = component.Buffs[index];
        return entry != null;
    }

    private static int FindIndex(UnitBuffRuntimeComponent component, in UnitSourceArguments arguments)
    {
        if (!arguments.TryGetInt(0, out int buffId) || component?.Buffs == null)
            return -1;

        for (int i = 0; i < component.Buffs.Count; i++)
        {
            if (component.Buffs[i]?.BuffId == buffId)
                return i;
        }

        return -1;
    }
}

public sealed class UnitBuffRuntimeEntry
{
    public int BuffId = -1;
    public float RemainingTime = -1f;
    public int StackCount = 1;
    public bool HasOriginEntity;
    public Entity OriginEntity = Entity.Null;
    public int SourceSkillId = -1;
    public List<PropertyModifierEntry> PropertyModifiers = new();
    public List<SkillModifierEntry> SkillModifiers = new();
    public List<BuffTriggerRuntimeEntry> TriggerEntries = new();
    public bool IsInitializedFromDefinition;

    public void InitializeFromDefinition(BuffData buffData, List<BuffTriggerRuntimeEntry> runtimeTriggerEntries = null)
    {
        if (buffData == null)
            return;

        PropertyModifiers = buffData.PropertyModifiers != null ? new List<PropertyModifierEntry>(buffData.PropertyModifiers) : new List<PropertyModifierEntry>();
        SkillModifiers = buffData.SkillModifiers != null ? new List<SkillModifierEntry>(buffData.SkillModifiers) : new List<SkillModifierEntry>();
        TriggerEntries = runtimeTriggerEntries ?? CreateRuntimeTriggers(buffData);
        IsInitializedFromDefinition = true;
    }

    public void EnsureDefinitionLoaded()
    {
        if (IsInitializedFromDefinition)
            return;

        BuffData buffData = DataComponent.Instance?.Get<BuffData>(BuffId);
        if (buffData == null)
            return;

        InitializeFromDefinition(buffData);
    }

    public bool Update(BuffUpdateContext context, float deltaTime)
    {
        EnsureDefinitionLoaded();

        bool hasInfiniteDuration = RemainingTime < 0f;
        float effectiveDeltaTime = deltaTime;
        if (!hasInfiniteDuration)
        {
            effectiveDeltaTime = Mathf.Min(deltaTime, RemainingTime);
            RemainingTime = Mathf.Max(0f, RemainingTime - deltaTime);
        }

        if (effectiveDeltaTime > 0f)
        {
            for (int i = 0; i < TriggerEntries.Count; i++)
            {
                BuffTriggerRuntimeEntry trigger = TriggerEntries[i];
                if (trigger.TriggerType != BuffTriggerType.Tick || trigger.TickIntervalSeconds <= 0f)
                    continue;

                if (trigger.NextTickTime <= 0f)
                    trigger.NextTickTime = trigger.TickIntervalSeconds;

                trigger.NextTickTime -= effectiveDeltaTime;
                if (trigger.NextTickTime > 0f)
                    continue;

                EnqueueEffects(
                    context?.EffectExecutionQueue,
                    trigger.RuntimeEffects,
                    SkillHookType.OnBuffTick,
                    context?.TargetEntity ?? Entity.Null);
                trigger.NextTickTime = trigger.TickIntervalSeconds;

                if (trigger.ConsumeStackOnTrigger && ConsumeOneStack())
                    break;
            }
        }

        return StackCount > 0 && (hasInfiniteDuration || RemainingTime > 0f);
    }

    public void ContributePropertyModifiers(PropertyModifierSet modifiers)
    {
        EnsureDefinitionLoaded();
        modifiers?.Add(PropertyModifiers, Mathf.Max(1, StackCount));
    }

    public void ContributeSkillModifiers(SkillModifierSet modifiers)
    {
        EnsureDefinitionLoaded();
        modifiers?.Add(SkillModifiers, Mathf.Max(1, StackCount));
    }

    public bool OnHook(BuffHookContext context)
    {
        EnsureDefinitionLoaded();
        if (context == null)
            return StackCount > 0;

        for (int i = 0; i < TriggerEntries.Count; i++)
        {
            BuffTriggerRuntimeEntry trigger = TriggerEntries[i];
            if (trigger.TriggerType != BuffTriggerType.Hook || trigger.HookType != context.HookType)
                continue;

            bool hasOtherEntity = context.HasOriginEntity || context.HasOtherEntity;
            Entity otherEntity = context.HasOriginEntity
                ? context.OriginEntity
                : context.HasOtherEntity ? context.OtherEntity : Entity.Null;
            EnqueueEffects(
                context?.EffectExecutionQueue,
                trigger.RuntimeEffects,
                context?.HookType ?? SkillHookType.None,
                context?.TargetEntity ?? Entity.Null,
                context?.TriggerValue ?? 0f,
                hasOtherEntity,
                otherEntity,
                context?.HasPosition ?? false,
                context?.Position ?? Vector3.zero);
            if (trigger.ConsumeStackOnTrigger && ConsumeOneStack())
                break;
        }

        return StackCount > 0;
    }

    private bool ConsumeOneStack()
    {
        StackCount = Mathf.Max(0, StackCount - 1);
        return StackCount <= 0;
    }

    private void EnqueueEffects(
        PendingEffectExecutionQueueComponent effectExecutionQueue,
        EffectData[] effects,
        SkillHookType hookType,
        Entity targetEntity,
        float triggerValue = 0f,
        bool hasOtherEntity = false,
        Entity otherEntity = default,
        bool hasPosition = false,
        Vector3 position = default)
    {
        if (effectExecutionQueue == null || effects == null || effects.Length == 0)
            return;

        effectExecutionQueue.Enqueue(new PendingEffectExecutionEntry
        {
            Effects = effects,
            TriggerSource = SkillTriggerSource.BuffHook,
            HookType = hookType,
            HasOriginEntity = HasOriginEntity,
            OriginEntity = HasOriginEntity ? OriginEntity : Entity.Null,
            SourceSkillId = SourceSkillId,
            HasTargetEntity = true,
            TargetEntity = targetEntity,
            HasOtherEntity = hasOtherEntity,
            OtherEntity = hasOtherEntity ? otherEntity : Entity.Null,
            HasPosition = hasPosition,
            Position = hasPosition ? position : Vector3.zero,
            TriggerValue = triggerValue,
            RepeatCount = Mathf.Max(1, StackCount),
        });
    }

    private static List<BuffTriggerRuntimeEntry> CreateRuntimeTriggers(BuffData buffData)
    {
        List<BuffTriggerEntry> configuredEntries = buffData.CreateEffectiveTriggerEntries();
        List<BuffTriggerRuntimeEntry> runtimeEntries = new(configuredEntries.Count);
        for (int i = 0; i < configuredEntries.Count; i++)
        {
            BuffTriggerEntry configuredEntry = configuredEntries[i];
            if (configuredEntry == null)
                continue;

            runtimeEntries.Add(new BuffTriggerRuntimeEntry
            {
                TriggerType = configuredEntry.TriggerType,
                TickIntervalSeconds = Mathf.Max(0f, configuredEntry.TickIntervalSeconds),
                NextTickTime = Mathf.Max(0f, configuredEntry.TickIntervalSeconds),
                HookType = configuredEntry.HookType,
                ConsumeStackOnTrigger = configuredEntry.ConsumeStackOnTrigger,
                RuntimeEffects = configuredEntry.Effects ?? Array.Empty<EffectData>(),
            });
        }

        return runtimeEntries;
    }
}

public class BuffUpdateContext
{
    public EntityManager EntityManager;
    public Entity TargetEntity = Entity.Null;
    public PendingEffectExecutionQueueComponent EffectExecutionQueue;
}

public class BuffHookContext
{
    public EntityManager EntityManager;
    public Entity TargetEntity = Entity.Null;
    public PendingEffectExecutionQueueComponent EffectExecutionQueue;
    public SkillHookType HookType;
    public SkillTriggerSource TriggerSource;
    public bool HasOriginEntity;
    public Entity OriginEntity = Entity.Null;
    public int SourceSkillId = -1;
    public bool HasOtherEntity;
    public Entity OtherEntity = Entity.Null;
    public bool HasPosition;
    public Vector3 Position = Vector3.zero;
    public float TriggerValue;
}

public sealed class BuffTriggerRuntimeEntry
{
    public BuffTriggerType TriggerType;
    public float TickIntervalSeconds;
    public float NextTickTime;
    public SkillHookType HookType;
    public bool ConsumeStackOnTrigger;
    public EffectData[] RuntimeEffects = Array.Empty<EffectData>();
}
