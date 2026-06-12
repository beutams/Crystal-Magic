using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class ApplyBuffEffect : Effect
    {
        public new ApplyBuffEffectData Data { get; }

        public ApplyBuffEffect(ApplyBuffEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity || Data.BuffId < 0)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null || !entityManager.Exists(target))
                return;

            BuffData buffData = DataComponent.Instance?.Get<BuffData>(Data.BuffId);
            if (buffData == null)
                return;

            UnitBuffRuntimeComponent runtimeComponent = UnitBuffUtility.GetOrCreateRuntimeComponent(entityManager, target);
            if (runtimeComponent == null)
                return;

            int stackToApply = math.max(1, Data.StackCount);
            float duration = Data.DurationSeconds < 0f ? -1f : math.max(0f, Data.DurationSeconds);
            System.Collections.Generic.List<BuffTriggerRuntimeEntry> runtimeTriggerEntries = CreateRuntimeTriggerEntries(context, buffData);
            bool hasOriginEntity = context.HasOriginEntity && context.OriginEntity != Entity.Null;
            Entity originEntity = hasOriginEntity ? context.OriginEntity : Entity.Null;
            int sourceSkillId = context.SourceSkillId;

            for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
                if (entry.BuffId != Data.BuffId)
                    continue;

                entry.RemainingTime = GetPreferredDuration(entry.RemainingTime, duration);
                entry.StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), math.max(1, entry.StackCount) + stackToApply)
                    : 1;
                entry.HasOriginEntity = hasOriginEntity;
                entry.OriginEntity = originEntity;
                entry.SourceSkillId = sourceSkillId;
                entry.InitializeFromDefinition(buffData, runtimeTriggerEntries);
                return;
            }

            UnitBuffRuntimeEntry newEntry = new()
            {
                BuffId = Data.BuffId,
                RemainingTime = duration,
                StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), stackToApply)
                    : 1,
                HasOriginEntity = hasOriginEntity,
                OriginEntity = originEntity,
                SourceSkillId = sourceSkillId,
            };
            newEntry.InitializeFromDefinition(buffData, runtimeTriggerEntries);
            runtimeComponent.Buffs.Add(newEntry);
        }

        private static System.Collections.Generic.List<BuffTriggerRuntimeEntry> CreateRuntimeTriggerEntries(SkillContent context, BuffData buffData)
        {
            if (buffData == null)
                return new System.Collections.Generic.List<BuffTriggerRuntimeEntry>();

            UnitElementComponent? originElementComponent = null;
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                context.EntityManager.Exists(context.OriginEntity) &&
                context.EntityManager.HasComponent<UnitElementComponent>(context.OriginEntity))
            {
                originElementComponent = context.EntityManager.GetComponentData<UnitElementComponent>(context.OriginEntity);
            }

            System.Collections.Generic.List<BuffTriggerEntry> configuredEntries = buffData.CreateEffectiveTriggerEntries();
            System.Collections.Generic.List<BuffTriggerRuntimeEntry> runtimeEntries = new(configuredEntries.Count);
            for (int i = 0; i < configuredEntries.Count; i++)
            {
                BuffTriggerEntry configuredEntry = configuredEntries[i];
                if (configuredEntry == null)
                    continue;

                runtimeEntries.Add(new BuffTriggerRuntimeEntry
                {
                    TriggerType = configuredEntry.TriggerType,
                    TickIntervalSeconds = math.max(0f, configuredEntry.TickIntervalSeconds),
                    NextTickTime = math.max(0f, configuredEntry.TickIntervalSeconds),
                    HookType = configuredEntry.HookType,
                    ConsumeStackOnTrigger = configuredEntry.ConsumeStackOnTrigger,
                    RuntimeEffects = EffectData.CreateRuntimeCopies(
                        configuredEntry.Effects,
                        context.RuntimeModifiers,
                        originElementComponent),
                });
            }

            return runtimeEntries;
        }

        private static float GetPreferredDuration(float currentDuration, float incomingDuration)
        {
            if (currentDuration < 0f || incomingDuration < 0f)
                return -1f;

            return math.max(currentDuration, incomingDuration);
        }
    }
}
