using CrystalMagic.Game.Data.Effects;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class SpawnProjectileEffect : Effect
    {
        public new SpawnProjectileEffectData Data { get; }

        public SpawnProjectileEffect(SpawnProjectileEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            if (!TryGetSpawnPosition(context, out Vector3 spawnPosition))
                return;

            if (!TryGetProjectileDirection(context, spawnPosition, out Vector3 direction))
            {
                Debug.LogWarning($"[SpawnProjectileEffect] Skill {context.SourceSkillId} requires a target position different from its spawn position.");
                return;
            }

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

        private static bool TryGetProjectileDirection(SkillContent context, Vector3 spawnPosition, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!context.HasPosition)
                return false;

            direction = context.Position - spawnPosition;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            direction.Normalize();
            return true;
        }

        private void CreateProjectileSpawnRequest(SkillContent context, Vector3 startPosition, Vector3 direction)
        {
            FixedString128Bytes projectileName = new(string.IsNullOrWhiteSpace(Data.ProjectilePrefabName) ? "Projectile" : Data.ProjectilePrefabName);
            SkillProjectileSpawnQueue.Enqueue(
                new SkillProjectileSpawnRequest
                {
                    ProjectileName = projectileName,
                    VisualPrefabName = new FixedString128Bytes(Data.VisualPrefabName ?? string.Empty),
                    StartPosition = new float3(startPosition.x, startPosition.y, startPosition.z),
                    Direction = new float3(direction.x, direction.y, direction.z),
                    Rotation = quaternion.identity,
                    VisualScale = Data.VisualScale,
                    VisualOffset = new float3(Data.VisualOffset.x, Data.VisualOffset.y, Data.VisualOffset.z),
                    Speed = Data.Speed,
                    MaxRange = Data.MaxRange,
                    HitRadius = math.max(Data.HitRadius, 0.01f),
                    CanPierce = Data.CanPierce ? (byte)1 : (byte)0,
                    TriggerDestroyEffectsOnMaxRange = Data.TriggerDestroyEffectsOnMaxRange ? (byte)1 : (byte)0,
                    Context = context.Clone(),
                    OnCollisionEffects = Data.OnCollisionEffects,
                    OnDestroyEffects = Data.OnDestroyEffects,
                });
        }
    }
}
