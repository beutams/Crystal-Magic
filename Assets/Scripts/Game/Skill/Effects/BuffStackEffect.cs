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
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !UnitBuffUtility.TryGetRuntimeComponent(entityManager, target, out UnitBuffRuntimeComponent runtimeComponent))
            {
                return;
            }

            int stackCount = GetBuffStackCount(runtimeComponent, Data.BuffId);
            if (stackCount <= 0)
                return;

            SkillContent childContext = context.Clone();
            childContext.RuntimeModifiers ??= new SkillModifierSet();
            childContext.RuntimeModifiers.Add(Data.PerStackModifiers, stackCount);
            SkillExecutor.ExecuteEffects(Data.OnAfterRead, childContext);
        }

        private static int GetBuffStackCount(UnitBuffRuntimeComponent runtimeComponent, int buffId)
        {
            if (runtimeComponent?.Buffs == null)
                return 0;

            for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
                if (entry.BuffId == buffId)
                    return math.max(0, entry.StackCount);
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
            if (Data == null || context == null || !context.HasTargetEntity || Data.BuffId < 0)
                return;

            EntityManager entityManager = context.EntityManager;
            Entity target = context.TargetEntity;
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !UnitBuffUtility.TryGetRuntimeComponent(entityManager, target, out UnitBuffRuntimeComponent runtimeComponent))
            {
                return;
            }

            for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
                if (entry.BuffId != Data.BuffId)
                    continue;

                if (Data.RemoveAllStacks)
                {
                    runtimeComponent.Buffs.RemoveAt(i);
                    return;
                }

                int remainStack = entry.StackCount - math.max(1, Data.RemoveStackCount);
                if (remainStack <= 0)
                    runtimeComponent.Buffs.RemoveAt(i);
                else
                    entry.StackCount = remainStack;

                return;
            }
        }
    }
}
