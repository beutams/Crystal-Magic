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
            bool hasTickEffect = buffData.TickIntervalSeconds > 0f && buffData.EffectChain != null && buffData.EffectChain.Length > 0;
            float nextTickTime = hasTickEffect
                ? buffData.TickIntervalSeconds
                : 0f;
            EffectData[] runtimeEffectChain = hasTickEffect
                ? CreateRuntimeEffectChain(context, buffData)
                : System.Array.Empty<EffectData>();
            bool hasOriginEntity = context.HasOriginEntity && context.OriginEntity != Entity.Null;
            Entity originEntity = hasOriginEntity ? context.OriginEntity : Entity.Null;
            int sourceSkillId = context.SourceSkillId;

            for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
                if (entry.BuffId != Data.BuffId)
                    continue;

                bool replaceRuntimeEffectChain = hasTickEffect &&
                    (entry.RuntimeEffectChain == null || entry.RuntimeEffectChain.Length == 0 || IsIncomingDurationLonger(entry.RemainingTime, duration));

                entry.RemainingTime = GetPreferredDuration(entry.RemainingTime, duration);
                entry.NextTickTime = nextTickTime;
                entry.StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), math.max(1, entry.StackCount) + stackToApply)
                    : 1;
                entry.HasOriginEntity = hasOriginEntity;
                entry.OriginEntity = originEntity;
                entry.SourceSkillId = sourceSkillId;
                entry.InitializeFromDefinition(buffData, replaceRuntimeEffectChain ? runtimeEffectChain : entry.RuntimeEffectChain);
                if (replaceRuntimeEffectChain)
                    entry.RuntimeEffectChain = runtimeEffectChain ?? System.Array.Empty<EffectData>();
                return;
            }

            UnitBuffRuntimeEntry newEntry = new()
            {
                BuffId = Data.BuffId,
                RemainingTime = duration,
                NextTickTime = nextTickTime,
                StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), stackToApply)
                    : 1,
                HasOriginEntity = hasOriginEntity,
                OriginEntity = originEntity,
                SourceSkillId = sourceSkillId,
                RuntimeEffectChain = runtimeEffectChain ?? System.Array.Empty<EffectData>(),
            };
            newEntry.InitializeFromDefinition(buffData, newEntry.RuntimeEffectChain);
            runtimeComponent.Buffs.Add(newEntry);
        }

        private static EffectData[] CreateRuntimeEffectChain(SkillContent context, BuffData buffData)
        {
            if (context == null || buffData?.EffectChain == null || buffData.EffectChain.Length == 0)
                return System.Array.Empty<EffectData>();

            UnitElementComponent? originElementComponent = null;
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                context.EntityManager.Exists(context.OriginEntity) &&
                context.EntityManager.HasComponent<UnitElementComponent>(context.OriginEntity))
            {
                originElementComponent = context.EntityManager.GetComponentData<UnitElementComponent>(context.OriginEntity);
            }

            return EffectData.CreateRuntimeCopies(
                buffData.EffectChain,
                context.RuntimeModifiers,
                originElementComponent);
        }

        private static float GetPreferredDuration(float currentDuration, float incomingDuration)
        {
            if (currentDuration < 0f || incomingDuration < 0f)
                return -1f;

            return math.max(currentDuration, incomingDuration);
        }

        private static bool IsIncomingDurationLonger(float currentDuration, float incomingDuration)
        {
            if (incomingDuration < 0f)
                return currentDuration >= 0f;

            if (currentDuration < 0f)
                return false;

            return incomingDuration > currentDuration;
        }
    }
}
