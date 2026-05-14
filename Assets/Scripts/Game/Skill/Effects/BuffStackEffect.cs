using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class ReadBuffStackEffect : Effect
    {
        public new ReadBuffStackEffectData Data { get; }

        public ReadBuffStackEffect(ReadBuffStackEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                !context.HasTargetEntity ||
                Data.BuffId < 0)
            {
                return;
            }

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasBuffer<UnitBuffElement>(target))
            {
                return;
            }

            int stackCount = GetBuffStackCount(entityManager.GetBuffer<UnitBuffElement>(target, true), Data.BuffId);
            if (stackCount <= 0)
                return;

            SkillContent childContext = context.Clone();
            childContext.RuntimeModifiers ??= new SkillModifierSet();
            childContext.RuntimeModifiers.Add(Data.PerStackModifiers, stackCount);
            SkillExecutor.ExecuteEffects(Data.OnAfterRead, childContext);
        }

        private static int GetBuffStackCount(DynamicBuffer<UnitBuffElement> buffer, int buffId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                UnitBuffElement element = buffer[i];
                if (element.BuffId == buffId)
                    return math.max(0, element.StackCount);
            }

            return 0;
        }
    }

    public sealed class RemoveBuffEffect : Effect
    {
        public new RemoveBuffEffectData Data { get; }

        public RemoveBuffEffect(RemoveBuffEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                !context.HasTargetEntity ||
                Data.BuffId < 0)
            {
                return;
            }

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasBuffer<UnitBuffElement>(target))
            {
                return;
            }

            DynamicBuffer<UnitBuffElement> buffer = entityManager.GetBuffer<UnitBuffElement>(target);
            for (int i = 0; i < buffer.Length; i++)
            {
                UnitBuffElement element = buffer[i];
                if (element.BuffId != Data.BuffId)
                    continue;

                if (Data.RemoveAllStacks)
                {
                    buffer.RemoveAt(i);
                    return;
                }

                int remainStack = element.StackCount - math.max(1, Data.RemoveStackCount);
                if (remainStack <= 0)
                    buffer.RemoveAt(i);
                else
                {
                    element.StackCount = remainStack;
                    buffer[i] = element;
                }

                return;
            }
        }
    }
}
