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

public sealed class UnitBuffRuntimeEntry
{
    public int BuffId = -1;
    public float RemainingTime = -1f;
    public float NextTickTime;
    public int StackCount = 1;
    public bool HasOriginEntity;
    public Entity OriginEntity = Entity.Null;
    public int SourceSkillId = -1;
    public int SourceExecutionToken = -1;
    public bool ConsumeOnDamageTaken;
    public int RemainingTriggerCount;
    public List<PropertyModifierEntry> PropertyModifiers = new();
    public List<SkillModifierEntry> SkillModifiers = new();
    public float TickIntervalSeconds;
    public EffectData[] RuntimeEffectChain = Array.Empty<EffectData>();
    public bool IsInitializedFromDefinition;

    public void InitializeFromDefinition(BuffData buffData, EffectData[] runtimeEffectChain = null)
    {
        if (buffData == null)
            return;
        PropertyModifiers = buffData.PropertyModifiers != null ? new List<PropertyModifierEntry>(buffData.PropertyModifiers) : new List<PropertyModifierEntry>();
        SkillModifiers = buffData.SkillModifiers != null ? new List<SkillModifierEntry>(buffData.SkillModifiers) : new List<SkillModifierEntry>();
        TickIntervalSeconds = Mathf.Max(0f, buffData.TickIntervalSeconds);
        RuntimeEffectChain = runtimeEffectChain ?? buffData.EffectChain ?? Array.Empty<EffectData>();
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

        if (TickIntervalSeconds > 0f && RuntimeEffectChain != null && RuntimeEffectChain.Length > 0 && effectiveDeltaTime > 0f)
        {
            if (NextTickTime <= 0f)
                NextTickTime = TickIntervalSeconds;

            NextTickTime -= effectiveDeltaTime;
            while (NextTickTime <= 0f)
            {
                context?.EffectExecutionQueue?.Enqueue(new PendingEffectExecutionEntry
                {
                    Effects = RuntimeEffectChain,
                    TriggerSource = SkillTriggerSource.BuffHook,
                    HookType = SkillHookType.OnBuffTick,
                    HasOriginEntity = HasOriginEntity,
                    OriginEntity = HasOriginEntity ? OriginEntity : Entity.Null,
                    SourceSkillId = SourceSkillId,
                    HasTargetEntity = true,
                    TargetEntity = context?.TargetEntity ?? Entity.Null,
                    RepeatCount = Mathf.Max(1, StackCount),
                });
                NextTickTime += TickIntervalSeconds;
            }
        }

        return hasInfiniteDuration || RemainingTime > 0f;
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

    public void OnHook(BuffHookContext context)
    {
        EnsureDefinitionLoaded();
    }
}
