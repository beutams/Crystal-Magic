using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class RectSearchEffect : Effect
    {
        private readonly List<UnitQueryHit> _hits = new();

        public new RectSearchEffectData Data { get; }

        public RectSearchEffect(RectSearchEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null ||
                context == null ||
                !context.HasOriginEntity ||
                !context.EntityManager.Exists(context.OriginEntity) ||
                !context.EntityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                return;
            }

            float2 size = math.max(float2.zero, new float2(Data.Size.x, Data.Size.y));
            if (math.any(size <= 0f) ||
                !UnitQueryUtility.TryGetTree(context.EntityManager, UnitQueryTreeKind.Unit, out UnitQueryTree unitTree))
            {
                return;
            }

            LocalTransform originTransform = context.EntityManager.GetComponentData<LocalTransform>(context.OriginEntity);
            float horizontalFacingSign = Data.UseHorizontalFacing
                ? GetHorizontalFacingSign(context.EntityManager, context.OriginEntity)
                : 1f;
            float3 center = originTransform.Position + new float3(
                Data.CenterOffset.x * horizontalFacingSign,
                Data.CenterOffset.y,
                Data.CenterOffset.z);
            unitTree.QueryAxisAlignedRect(center, size, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (!EffectConditionUtility.Pass(Data.TargetConditions, context, hit.Entity))
                    continue;

                Vector3 hitPosition = new(hit.Position.x, hit.Position.y, hit.Position.z);
                SkillContent targetContext = context.CloneForTarget(hit.Entity, hitPosition);
                targetContext.EntityManager = context.EntityManager;
                SkillExecutor.ExecuteEffects(Data.OnAfterSearch, targetContext);
            }
        }

        private static float GetHorizontalFacingSign(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<UnitAnimationComponent>(entity))
            {
                UnitAnimationComponent animation = entityManager.GetComponentObject<UnitAnimationComponent>(entity);
                if (animation != null)
                    return animation.LastTwoDirectionFacing == UnitAnimationDirection.Left ? -1f : 1f;
            }

            return UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facing) && facing.x < -0.0001f
                ? -1f
                : 1f;
        }
    }
}
