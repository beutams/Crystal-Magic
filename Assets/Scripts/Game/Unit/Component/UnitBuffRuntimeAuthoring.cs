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

            UnitBuffModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitBuffModuleData>(authoring);
            if (data?.Buffs == null)
                return;

            for (int i = 0; i < data.Buffs.Count; i++)
            {
                UnitInitialBuffEntry entry = data.Buffs[i];
                if (entry == null || entry.BuffId < 0)
                    continue;

                runtimeComponent.Buffs.Add(new UnitBuffRuntimeEntry
                {
                    BuffId = entry.BuffId,
                    RemainingTime = entry.DurationSeconds,
                    StackCount = Mathf.Max(1, entry.StackCount),
                    HasOriginEntity = false,
                    OriginEntity = Entity.Null,
                    SourceSkillId = -1,
                });
            }
        }
    }
}

public class UnitBuffRuntimeComponent : IComponentData
{
    public List<UnitBuffRuntimeEntry> Buffs = new();
}

[UnitSourceAuthoring(typeof(UnitBuffRuntimeAuthoring))]
public sealed class UnitBuffSource : UnitManagedComponentSource<UnitBuffRuntimeComponent>
{
    private static readonly ComparatorParameterDefinition[] s_indexParameter =
    {
        new ComparatorParameterDefinition("Index", UnitValueCategory.Number),
    };

    private static readonly ComparatorParameterDefinition[] s_buffIdParameter =
    {
        new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number),
    };

    private static readonly ComparatorParameterDefinition[] s_addParameters =
    {
        new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Duration", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Stacks", UnitValueCategory.Number),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitBuffRuntimeComponent> builder)
    {
        builder.AddGet("unit.buffs.count", UnitValueCategory.Number,
            (in UnitBuffRuntimeComponent component) => UnitValue.FromInt(component.Buffs?.Count ?? 0));
        builder.AddGet("unit.buffs.idAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => GetEntry(component, input, out UnitBuffRuntimeEntry entry) ? UnitValue.FromInt(entry.BuffId) : UnitValue.None);
        builder.AddGet("unit.buffs.remainingTimeAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => GetEntry(component, input, out UnitBuffRuntimeEntry entry) ? UnitValue.FromFloat(entry.RemainingTime) : UnitValue.None);
        builder.AddGet("unit.buffs.stackCountAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => GetEntry(component, input, out UnitBuffRuntimeEntry entry) ? UnitValue.FromInt(entry.StackCount) : UnitValue.None);
        builder.AddGet("unit.buffs.hasOriginAt", UnitValueCategory.Bool, s_indexParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => GetEntry(component, input, out UnitBuffRuntimeEntry entry) ? UnitValue.FromBool(entry.HasOriginEntity) : UnitValue.None);
        builder.AddGet("unit.buffs.originEntityAt", UnitValueCategory.Entity, s_indexParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => GetEntry(component, input, out UnitBuffRuntimeEntry entry) ? UnitValue.FromEntity(entry.OriginEntity) : UnitValue.None);
        builder.AddGet("unit.buffs.sourceSkillIdAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => GetEntry(component, input, out UnitBuffRuntimeEntry entry) ? UnitValue.FromInt(entry.SourceSkillId) : UnitValue.None);
        builder.AddGet("unit.buffs.findIndex", UnitValueCategory.Number, s_buffIdParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => UnitValue.FromInt(FindIndex(component, input)));
        builder.AddGet("unit.buffs.has", UnitValueCategory.Bool, s_buffIdParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => UnitValue.FromBool(FindIndex(component, input) >= 0));
        builder.AddGet("unit.buffs.stackCount", UnitValueCategory.Number, s_buffIdParameter,
            (in UnitBuffRuntimeComponent component, UnitValue[] input) => UnitValue.FromInt(GetStackCount(component, input)));

        builder.AddSet("unit.buffs.add", s_addParameters,
            (ref UnitBuffRuntimeComponent component, UnitValue[] input) => Add(component, input));
        builder.AddSet("unit.buffs.remove", s_buffIdParameter,
            (ref UnitBuffRuntimeComponent component, UnitValue[] input) => Remove(component, input));
        builder.AddSet("unit.buffs.clear", Array.Empty<ComparatorParameterDefinition>(),
            (ref UnitBuffRuntimeComponent component, UnitValue[] _) => Clear(component));
    }

    private static bool GetEntry(UnitBuffRuntimeComponent component, UnitValue[] input, out UnitBuffRuntimeEntry entry)
    {
        entry = null;
        if (!TryGetInt(input, 0, out int index) || component?.Buffs == null || index < 0 || index >= component.Buffs.Count)
            return false;

        entry = component.Buffs[index];
        return entry != null;
    }

    private static int FindIndex(UnitBuffRuntimeComponent component, UnitValue[] input)
    {
        if (!TryGetInt(input, 0, out int buffId) || component?.Buffs == null)
            return -1;

        for (int i = 0; i < component.Buffs.Count; i++)
        {
            if (component.Buffs[i]?.BuffId == buffId)
                return i;
        }

        return -1;
    }

    private static int GetStackCount(UnitBuffRuntimeComponent component, UnitValue[] input)
    {
        int index = FindIndex(component, input);
        return index >= 0 ? component.Buffs[index].StackCount : 0;
    }

    private static bool Add(UnitBuffRuntimeComponent component, UnitValue[] input)
    {
        if (!TryGetInt(input, 0, out int buffId) ||
            !TryGetNumber(input, 1, out float duration) ||
            !TryGetInt(input, 2, out int stacks) ||
            component == null ||
            buffId < 0)
        {
            return false;
        }

        BuffData buffData = DataComponent.Instance?.Get<BuffData>(buffId);
        if (buffData == null)
            return false;

        component.Buffs ??= new List<UnitBuffRuntimeEntry>();
        duration = duration < 0f ? -1f : Mathf.Max(0f, duration);
        stacks = Mathf.Max(1, stacks);

        for (int i = 0; i < component.Buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = component.Buffs[i];
            if (entry == null || entry.BuffId != buffId)
                continue;

            entry.RemainingTime = GetPreferredDuration(entry.RemainingTime, duration);
            entry.StackCount = buffData.CanStack
                ? Mathf.Min(Mathf.Max(1, buffData.MaxStacks), Mathf.Max(1, entry.StackCount) + stacks)
                : 1;
            entry.HasOriginEntity = false;
            entry.OriginEntity = Entity.Null;
            entry.SourceSkillId = -1;
            entry.InitializeFromDefinition(buffData);
            return true;
        }

        UnitBuffRuntimeEntry newEntry = new()
        {
            BuffId = buffId,
            RemainingTime = duration,
            StackCount = buffData.CanStack ? Mathf.Min(Mathf.Max(1, buffData.MaxStacks), stacks) : 1,
            HasOriginEntity = false,
            OriginEntity = Entity.Null,
            SourceSkillId = -1,
        };
        newEntry.InitializeFromDefinition(buffData);
        component.Buffs.Add(newEntry);
        return true;
    }

    private static bool Remove(UnitBuffRuntimeComponent component, UnitValue[] input)
    {
        if (!TryGetInt(input, 0, out int buffId) || component?.Buffs == null)
            return false;

        bool removed = false;
        for (int i = component.Buffs.Count - 1; i >= 0; i--)
        {
            if (component.Buffs[i]?.BuffId != buffId)
                continue;

            component.Buffs.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    private static bool Clear(UnitBuffRuntimeComponent component)
    {
        if (component?.Buffs == null || component.Buffs.Count == 0)
            return false;

        component.Buffs.Clear();
        return true;
    }

    private static float GetPreferredDuration(float currentDuration, float incomingDuration)
    {
        return currentDuration < 0f || incomingDuration < 0f ? -1f : Mathf.Max(currentDuration, incomingDuration);
    }

    private static bool TryGetInt(UnitValue[] input, int index, out int value)
    {
        value = 0;
        return TryGetNumber(input, index, out float number) && TryConvertToInt(number, out value);
    }

    private static bool TryGetNumber(UnitValue[] input, int index, out float value)
    {
        value = 0f;
        return input != null && index >= 0 && index < input.Length && input[index].TryGetNumber(out value);
    }

    private static bool TryConvertToInt(float value, out int result)
    {
        result = Mathf.RoundToInt(value);
        return Mathf.Abs(value - result) <= 0.0001f;
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
