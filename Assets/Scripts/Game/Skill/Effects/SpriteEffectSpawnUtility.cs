using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.Game.Skill.Effects
{
    internal static class SpriteEffectSpawnUtility
    {
        public static bool TrySpawn(
            EntityManager entityManager,
            string prefabName,
            float3 position,
            quaternion rotation,
            float scale,
            float loopDuration,
            out Entity entity)
        {
            return TrySpawn(
                entityManager,
                prefabName,
                position,
                rotation,
                scale,
                loopDuration,
                false,
                out entity);
        }

        public static bool TrySpawn(
            EntityManager entityManager,
            string prefabName,
            float3 position,
            quaternion rotation,
            float scale,
            float loopDuration,
            bool preservePrefabRotation,
            out Entity entity)
        {
            if (!EntitySpawnRegistryUtility.TryInstantiateVfx(entityManager, new FixedString128Bytes(prefabName), out entity))
                return false;

            LocalTransform transform = preservePrefabRotation && entityManager.HasComponent<LocalTransform>(entity)
                ? entityManager.GetComponentData<LocalTransform>(entity)
                : LocalTransform.FromPositionRotationScale(position, rotation, math.max(0.01f, scale));
            transform.Position = position;
            transform.Scale = math.max(0.01f, scale);
            SetOrAddComponentData(entityManager, entity, transform);

            if (entityManager.HasComponent<SpriteEffectAnimationComponent>(entity))
            {
                SpriteEffectAnimationComponent animation = entityManager.GetComponentObject<SpriteEffectAnimationComponent>(entity);
                animation.RemainingLoopSeconds = loopDuration > 0f ? loopDuration : -1f;
            }
            else if (loopDuration > 0f)
            {
                SetOrAddComponentData(entityManager, entity, new VfxLifetimeComponent { RemainingSeconds = loopDuration });
            }

            return true;
        }

        public static bool TryGetReleasePosition(SkillContent context, out float3 position)
        {
            if (context.HasPosition)
            {
                position = new float3(context.Position.x, context.Position.y, context.Position.z);
                return true;
            }

            if (TryGetEntityTransform(context, context.HasTargetEntity, context.TargetEntity, out LocalTransform targetTransform))
            {
                position = targetTransform.Position;
                return true;
            }

            if (TryGetEntityTransform(context, context.HasOriginEntity, context.OriginEntity, out LocalTransform originTransform))
            {
                position = originTransform.Position;
                return true;
            }

            position = float3.zero;
            return false;
        }

        public static bool TryGetFollowTarget(
            SkillContent context,
            bool useTarget,
            out Entity target,
            out LocalTransform transform)
        {
            bool hasEntity = useTarget ? context.HasTargetEntity : context.HasOriginEntity;
            Entity candidate = useTarget ? context.TargetEntity : context.OriginEntity;
            if (TryGetEntityTransform(context, hasEntity, candidate, out transform))
            {
                target = candidate;
                return true;
            }

            target = Entity.Null;
            return false;
        }

        public static quaternion GetFacingRotation(SkillContent context, bool alignToOrigin)
        {
            if (!alignToOrigin ||
                !context.HasOriginEntity ||
                !UnitFacingUtility.TryGetFacing(context.EntityManager, context.OriginEntity, out float2 facing))
            {
                return quaternion.identity;
            }

            return UnitFacingUtility.CreateRotation(facing);
        }

        public static quaternion GetEntityFacingRotation(EntityManager entityManager, Entity entity, bool align)
        {
            if (!align || !UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facing))
                return quaternion.identity;

            return UnitFacingUtility.CreateRotation(facing);
        }

        public static void SetOrAddComponentData<T>(EntityManager entityManager, Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                entityManager.SetComponentData(entity, value);
            else
                entityManager.AddComponentData(entity, value);
        }

        private static bool TryGetEntityTransform(SkillContent context, bool hasEntity, Entity entity, out LocalTransform transform)
        {
            if (hasEntity &&
                entity != Entity.Null &&
                context.EntityManager.Exists(entity) &&
                context.EntityManager.HasComponent<LocalTransform>(entity))
            {
                transform = context.EntityManager.GetComponentData<LocalTransform>(entity);
                return true;
            }

            transform = default;
            return false;
        }
    }
}
