using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    public abstract class Skill
    {
        protected Skill(ResolvedSkillData data)
        {
            Data = data;
        }

        protected ResolvedSkillData Data { get; }

        public virtual bool TryExecute(
            EntityManager entityManager,
            Entity entity,
            in UnitCastComponent cast,
            SkillContent context,
            SkillModifierSet runtimeModifiers = null)
        {
            if (context == null)
                return false;

            ResetContext(context, entityManager, entity, runtimeModifiers);
            if (!BuildContext(entityManager, entity, cast, context))
                return false;

            Execute(context);
            return true;
        }

        protected virtual bool BuildContext(EntityManager entityManager, Entity entity, in UnitCastComponent cast, SkillContent context)
        {
            return true;
        }

        protected virtual void Execute(SkillContent context)
        {
            SkillExecutor.ExecuteSkill(Data, context);
        }

        protected static void SetPosition(SkillContent context, bool hasPosition, Vector3 position)
        {
            context.HasPosition = hasPosition;
            context.Position = position;
        }

        protected static void SetTargetEntity(SkillContent context, bool hasTargetEntity, Entity targetEntity)
        {
            context.HasTargetEntity = hasTargetEntity;
            context.TargetEntity = targetEntity;
        }

        private static void ResetContext(
            SkillContent context,
            EntityManager entityManager,
            Entity entity,
            SkillModifierSet runtimeModifiers)
        {
            context.EntityManager = entityManager;
            context.HasOriginEntity = true;
            context.OriginEntity = entity;
            context.HasTargetEntity = false;
            context.TargetEntity = Entity.Null;
            context.HasTarget = false;
            context.Target = null;
            context.Origin = null;
            context.HasPosition = false;
            context.Position = Vector3.zero;
            context.RuntimeModifiers = runtimeModifiers?.Clone();
        }
    }
}
