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
            float duration = math.max(0f, Data.DurationSeconds);

            for (int i = 0; i < buffer.Length; i++)
            {
                UnitBuffElement element = buffer[i];
                if (element.BuffId != Data.BuffId)
                    continue;

                element.RemainingTime = duration;
                element.StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), math.max(1, element.StackCount) + stackToApply)
                    : 1;
                buffer[i] = element;
                return;
            }

            buffer.Add(new UnitBuffElement
            {
                BuffId = Data.BuffId,
                RemainingTime = duration,
                StackCount = buffData.CanStack
                    ? math.min(math.max(1, buffData.MaxStacks), stackToApply)
                    : 1,
            });
        }
    }
}
