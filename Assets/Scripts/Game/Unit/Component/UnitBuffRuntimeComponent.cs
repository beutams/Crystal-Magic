using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

public class UnitBuffRuntimeComponent : IComponentData
{
    public List<UnitBuffRuntimeEntry> Buffs = new();
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

            EnqueueEffects(
                context?.EffectExecutionQueue,
                trigger.RuntimeEffects,
                context?.HookType ?? SkillHookType.None,
                context?.TargetEntity ?? Entity.Null,
                context?.TriggerValue ?? 0f,
                context?.HasOtherEntity ?? false,
                context?.OtherEntity ?? Entity.Null,
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
