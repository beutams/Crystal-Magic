using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class PersistentEffect : Effect
    {
        public new PersistentEffectData Data { get; }

        public PersistentEffect(PersistentEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            if (!TryGetReleasePosition(context, context.EntityManager, out Vector3 position))
                return;

            SkillContent persistentContext = context.Clone();
            CaptureOriginPositionSnapshot(persistentContext, context.EntityManager);
            PersistentEffectUtility.AddEffect(Data, persistentContext, position);
        }

        private static void CaptureOriginPositionSnapshot(SkillContent context, EntityManager entityManager)
        {
            if (context.HasOriginPositionSnapshot ||
                !context.HasOriginEntity ||
                context.OriginEntity == Entity.Null ||
                !entityManager.Exists(context.OriginEntity) ||
                !entityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                return;
            }

            Unity.Mathematics.float3 position = entityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
            context.HasOriginPositionSnapshot = true;
            context.OriginPositionSnapshot = new Vector3(position.x, position.y, position.z);
        }

        private static bool TryGetReleasePosition(SkillContent context, EntityManager entityManager, out Vector3 position)
        {
            if (context.HasPosition)
            {
                position = context.Position;
                return true;
            }

            if (TryGetEntityPosition(context.HasTargetEntity, context.TargetEntity, entityManager, out position))
                return true;

            if (TryGetEntityPosition(context.HasOriginEntity, context.OriginEntity, entityManager, out position))
                return true;

            position = Vector3.zero;
            return false;
        }

        private static bool TryGetEntityPosition(bool hasEntity, Entity entity, EntityManager entityManager, out Vector3 position)
        {
            if (hasEntity &&
                entity != Entity.Null &&
                entityManager.Exists(entity) &&
                entityManager.HasComponent<LocalTransform>(entity))
            {
                Unity.Mathematics.float3 entityPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;
                position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z);
                return true;
            }

            position = Vector3.zero;
            return false;
        }
    }
}
