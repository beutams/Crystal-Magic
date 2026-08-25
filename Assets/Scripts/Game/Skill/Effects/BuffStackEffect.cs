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
            if (Data == null || context == null || !context.HasTargetEntity || Data.BuffId < 0)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null || !entityManager.Exists(target))
                return;

            int stackCount = UnitBuffUtility.GetStackCount(entityManager, target, Data.BuffId);
            if (stackCount <= 0)
                return;

            SkillContent childContext = context.Clone();
            childContext.RuntimeModifiers ??= new SkillModifierSet();
            childContext.RuntimeModifiers.Add(Data.PerStackModifiers, stackCount);
            SkillExecutor.ExecuteEffects(Data.OnAfterRead, childContext);
        }
    }

    public sealed class RemoveBuffEffect : Effect
    {
        public new RemoveBuffEffectData Data { get; }

        public RemoveBuffEffect(RemoveBuffEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || !context.HasTargetEntity || Data.BuffId < 0)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null || !entityManager.Exists(target))
                return;

            UnitBuffUtility.Remove(
                entityManager,
                target,
                Data.BuffId,
                Data.RemoveAllStacks,
                Data.RemoveStackCount);
        }
    }
}
