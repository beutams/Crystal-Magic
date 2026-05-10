using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// Creates a projectile spawn request that will be materialized by the ECS spawn system.
    /// </summary>
    public sealed class SpawnProjectileEffect : Effect
    {
        public new SpawnProjectileEffectData Data { get; }

        public SpawnProjectileEffect(SpawnProjectileEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || Data.Projectile == null || context == null)
                return;

            if (!TryGetSpawnPosition(context, out Vector3 spawnPosition))
                return;

            Vector3 direction = GetProjectileDirection(context, spawnPosition);
            Vector3 finalPosition = spawnPosition + direction * Data.SpawnOffsetDistance;
            CreateProjectileSpawnRequest(context, finalPosition, direction);
        }

        private bool TryGetSpawnPosition(SkillContent context, out Vector3 position)
        {
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                context.EntityManager.Exists(context.OriginEntity) &&
                context.EntityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                float3 entityPosition = context.EntityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
                position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z);
                return true;
            }

            if (context.HasPosition)
            {
                position = context.Position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static Vector3 GetProjectileDirection(SkillContent context, Vector3 spawnPosition)
        {
            if (context.HasPosition)
            {
                Vector3 direction = context.Position - spawnPosition;
                direction.z = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    return direction.normalized;
            }

            return Vector3.right;
        }

        private void CreateProjectileSpawnRequest(SkillContent context, Vector3 startPosition, Vector3 direction)
        {
            EntityManager entityManager = context.EntityManager;
            Entity requestEntity = entityManager.CreateEntity(typeof(SkillProjectileSpawnRequestComponent));

            entityManager.SetComponentData(
                requestEntity,
                new SkillProjectileSpawnRequestComponent
                {
                    StartPosition = new float3(startPosition.x, startPosition.y, startPosition.z),
                    Direction = new float3(direction.x, direction.y, direction.z),
                    Speed = Data.Speed,
                    MaxRange = Data.MaxRange,
                    HitRadius = 0.75f * math.max(Data.Scale, 0.01f),
                    ScaleMultiplier = math.max(Data.Scale, 0.01f),
                    CanPierce = Data.CanPierce ? (byte)1 : (byte)0,
                    TriggerDestroyEffectsOnMaxRange = Data.TriggerDestroyEffectsOnMaxRange ? (byte)1 : (byte)0,
                });

            entityManager.AddComponentObject(
                requestEntity,
                new SkillProjectilePayloadComponent
                {
                    ProjectilePrefab = Data.Projectile,
                    Context = context.Clone(),
                    OnCollisionEffects = Data.OnCollisionEffects,
                    OnDestroyEffects = Data.OnDestroyEffects,
                });
        }
    }
}
