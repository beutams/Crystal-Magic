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

            if (!entityManager.HasBuffer<UnitBuffElement>(target))
                return;

            BuffData buffData = DataComponent.Instance?.Get<BuffData>(Data.BuffId);
            if (buffData == null)
                return;

            DynamicBuffer<UnitBuffElement> buffer = entityManager.GetBuffer<UnitBuffElement>(target);
            int stackToApply = math.max(1, Data.StackCount);
            float duration = Data.DurationSeconds < 0f ? -1f : math.max(0f, Data.DurationSeconds);
            float nextTickTime = buffData is EffectBuffData effectBuffData && effectBuffData.TickIntervalSeconds > 0f
                ? effectBuffData.TickIntervalSeconds
                : 0f;
            bool isEffectBuff = buffData is EffectBuffData;
            EffectData[] runtimeEffectChain = isEffectBuff
                ? CreateRuntimeEffectChain(context, effectBuffData)
                : null;
            bool hasOriginEntity = context.HasOriginEntity && context.OriginEntity != Entity.Null;
            Entity originEntity = hasOriginEntity ? context.OriginEntity : Entity.Null;

            for (int i = 0; i < buffer.Length; i++)
            {
                UnitBuffElement element = buffer[i];
                if (element.BuffId != Data.BuffId)
                    continue;

                bool replaceRuntimePayload = isEffectBuff &&
                    (element.RuntimePayloadId < 0 || IsIncomingDurationLonger(element.RemainingTime, duration));

                element.RemainingTime = GetPreferredDuration(element.RemainingTime, duration);
                element.NextTickTime = nextTickTime;
                element.StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), math.max(1, element.StackCount) + stackToApply)
                    : 1;

                if (replaceRuntimePayload)
                {
                    element.RuntimePayloadId = UnitBuffUtility.SetEffectBuffPayload(
                        entityManager,
                        target,
                        element.RuntimePayloadId,
                        runtimeEffectChain,
                        hasOriginEntity,
                        originEntity);
                }

                buffer[i] = element;
                return;
            }

            int runtimePayloadId = isEffectBuff
                ? UnitBuffUtility.SetEffectBuffPayload(
                    entityManager,
                    target,
                    -1,
                    runtimeEffectChain,
                    hasOriginEntity,
                    originEntity)
                : -1;

            buffer.Add(new UnitBuffElement
            {
                BuffId = Data.BuffId,
                RemainingTime = duration,
                NextTickTime = nextTickTime,
                RuntimePayloadId = runtimePayloadId,
                StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), stackToApply)
                    : 1,
            });
        }

        private static EffectData[] CreateRuntimeEffectChain(SkillContent context, EffectBuffData effectBuffData)
        {
            if (context == null || effectBuffData?.EffectChain == null || effectBuffData.EffectChain.Length == 0)
                return System.Array.Empty<EffectData>();

            return EffectData.CreateRuntimeCopies(
                effectBuffData.EffectChain,
                context.RuntimeModifiers,
                effectData => ResolveOriginElementBonus(context, effectData));
        }

        private static float ResolveOriginElementBonus(SkillContent context, EffectData effectData)
        {
            if (context == null ||
                effectData == null ||
                !context.HasOriginEntity ||
                context.OriginEntity == Entity.Null)
            {
                return 0f;
            }

            EntityManager entityManager = context.EntityManager;
            if (!entityManager.Exists(context.OriginEntity) ||
                !entityManager.HasComponent<UnitElementComponent>(context.OriginEntity))
            {
                return 0f;
            }

            ElementType element = effectData switch
            {
                DamageEffectData damageEffectData => damageEffectData.Element,
                PersistentEffectData persistentEffectData => persistentEffectData.Element,
                _ => ElementType.None,
            };

            if (element == ElementType.None)
                return 0f;

            return entityManager.GetComponentData<UnitElementComponent>(context.OriginEntity).GetPowerBonus(element);
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
