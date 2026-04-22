using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 持续性效果（Buff / 场地效果），逻辑由持久化系统接入
    /// </summary>
    public sealed class PersistentEffect : Effect
    {
        public new PersistentEffectData Data { get; }

        public PersistentEffect(PersistentEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            PersistentEffectSystem system = PersistentEffectSystem.Default;
            if (system == null || Data == null || context == null)
                return;

            if (!TryGetReleasePosition(context, system.EffectEntityManager, out Vector3 position))
                return;

            system.AddEffect(Data, context, position);
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
