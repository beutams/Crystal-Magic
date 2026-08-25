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

            int stackToApply = math.max(1, Data.StackCount);
            float duration = Data.DurationSeconds < 0f ? -1f : math.max(0f, Data.DurationSeconds);
            System.Collections.Generic.List<BuffTriggerRuntimeEntry> runtimeTriggerEntries = CreateRuntimeTriggerEntries(context, buffData);
            bool hasOriginEntity = context.HasOriginEntity && context.OriginEntity != Entity.Null;
            Entity originEntity = hasOriginEntity ? context.OriginEntity : Entity.Null;
            int sourceSkillId = context.SourceSkillId;
            UnitBuffUtility.Apply(
                entityManager,
                target,
                Data.BuffId,
                duration,
                stackToApply,
                originEntity,
                sourceSkillId,
                runtimeTriggerEntries);
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
    }
}
