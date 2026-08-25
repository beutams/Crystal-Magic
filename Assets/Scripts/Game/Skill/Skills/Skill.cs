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
            in SkillReleaseRequest request,
            SkillContent context)
        {
            if (context == null)
                return false;

            ResetContext(context, entityManager, request);
            context.SourceSkillId = Data != null ? Data.Id : -1;
            if (!BuildContext(request, context))
                return false;

            Execute(context);
            return true;
        }

        protected virtual bool BuildContext(in SkillReleaseRequest request, SkillContent context)
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

        protected static void SetOtherEntity(SkillContent context, bool hasOtherEntity, Entity otherEntity)
        {
            context.HasOtherEntity = hasOtherEntity;
            context.OtherEntity = otherEntity;
        }

        private static void ResetContext(
            SkillContent context,
            EntityManager entityManager,
            in SkillReleaseRequest request)
        {
            context.EntityManager = entityManager;
            context.HasOriginEntity = true;
            context.OriginEntity = request.OriginEntity;
            context.HasTargetEntity = false;
            context.TargetEntity = Entity.Null;
            context.HasTarget = false;
            context.Target = null;
            context.Origin = null;
            context.HasPosition = false;
            context.Position = Vector3.zero;
            context.RuntimeModifiers = null;
            context.TriggerSource = SkillTriggerSource.ActiveCast;
            context.HookType = SkillHookType.None;
            context.HasOtherEntity = false;
            context.OtherEntity = Entity.Null;
            context.TriggerValue = 0f;
        }
    }
}
