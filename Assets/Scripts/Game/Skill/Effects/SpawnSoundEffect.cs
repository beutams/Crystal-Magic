using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 生成音效效果，逻辑由音频系统接入
    /// </summary>
    public sealed class SpawnSoundEffect : Effect
    {
        public new SpawnSoundEffectData Data { get; }

        public SpawnSoundEffect(SpawnSoundEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || AudioComponent.Instance == null)
                return;

            switch (Data.Channel)
            {
                case AudioChannel.BGM:
                    AudioComponent.Instance.PlayBGM(Data.AudioPath, Data.Volume);
                    break;

                case AudioChannel.UI:
                    AudioComponent.Instance.PlayUI(Data.AudioPath, Data.Volume, Data.Pitch, Data.DelaySeconds);
                    break;

                default:
                    PlayUnitSound(context);
                    break;
            }
        }

        private void PlayUnitSound(SkillContent context)
        {
            if (Data.FollowCaster && context.HasOriginEntity)
            {
                AudioComponent.Instance.PlayUnitFollowEntity(
                    Data.AudioPath,
                    context.OriginEntity,
                    context.EntityManager,
                    Vector3.zero,
                    Data.Volume,
                    Data.Pitch,
                    Data.SpatialBlend,
                    Data.DelaySeconds);
                return;
            }

            Vector3 position = TryGetReleasePosition(context, out Vector3 releasePosition)
                ? releasePosition
                : Vector3.zero;

            AudioComponent.Instance.PlayUnit(
                Data.AudioPath,
                position,
                Data.Volume,
                Data.Pitch,
                Data.SpatialBlend,
                Data.DelaySeconds);
        }

        private static bool TryGetReleasePosition(SkillContent context, out Vector3 position)
        {
            if (context.HasPosition)
            {
                position = context.Position;
                return true;
            }

            if (TryGetEntityPosition(context.HasTargetEntity, context.TargetEntity, context.EntityManager, out position))
                return true;

            if (TryGetEntityPosition(context.HasOriginEntity, context.OriginEntity, context.EntityManager, out position))
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
