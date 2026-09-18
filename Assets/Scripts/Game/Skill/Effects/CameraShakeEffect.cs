using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class CameraShakeEffect : Effect
    {
        public new CameraShakeEffectData Data { get; }

        public CameraShakeEffect(CameraShakeEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            Vector3 position = TryGetShakePosition(context, out Vector3 shakePosition)
                ? shakePosition
                : Vector3.zero;

            Vector3 finalPosition = position + Data.PositionOffset;
            if (NetworkPresentationEventUtility.TryEnqueueCameraShake(
                    context.EntityManager,
                    new Unity.Mathematics.float3(finalPosition.x, finalPosition.y, finalPosition.z),
                    Data.Duration,
                    Data.Amplitude,
                    Data.Frequency,
                    Data.UseDistanceAttenuation,
                    Data.Radius))
            {
                return;
            }

            if (CameraComponent.Instance == null)
                return;

            CameraComponent.Instance.AddShake(
                finalPosition,
                Data.Duration,
                Data.Amplitude,
                Data.Frequency,
                Data.UseDistanceAttenuation,
                Data.Radius);
        }

        private static bool TryGetShakePosition(SkillContent context, out Vector3 position)
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
