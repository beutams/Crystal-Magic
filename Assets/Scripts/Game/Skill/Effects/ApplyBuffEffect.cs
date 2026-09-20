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
            Entity target = Data.TargetSource == BuffTargetSource.OtherEntity && context.HasOtherEntity
                ? context.OtherEntity
                : context.TargetEntity;
            if (target == Entity.Null || !entityManager.Exists(target))
                return;

            if (Data.OnlyOncePerPersistentEffect &&
                context.PersistentEffectAppliedBuffTargets != null &&
                !TryRegisterPersistentBuffTarget(context.PersistentEffectAppliedBuffTargets, Data.BuffId, target))
            {
                return;
            }

            int stackToApply = math.max(1, Data.StackCount);
            float duration = Data.DurationSeconds < 0f ? -1f : math.max(0f, Data.DurationSeconds);
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
                sourceSkillId);
        }

        private static bool TryRegisterPersistentBuffTarget(
            System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<Entity>> targetsByBuff,
            int buffId,
            Entity target)
        {
            if (!targetsByBuff.TryGetValue(buffId, out System.Collections.Generic.HashSet<Entity> targets))
            {
                targets = new System.Collections.Generic.HashSet<Entity>();
                targetsByBuff.Add(buffId, targets);
            }

            return targets.Add(target);
        }

    }
}
