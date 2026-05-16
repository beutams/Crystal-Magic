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
            FixedString128Bytes projectileName = new FixedString128Bytes(ProjectileVisualUtility.GenericProjectilePrefabName);
            SkillProjectileSpawnQueue.Enqueue(
                new SkillProjectileSpawnRequest
                {
                    Kind = SkillProjectileSpawnRequestKind.Projectile,
                    ProjectileName = projectileName,
                    StartPosition = new float3(startPosition.x, startPosition.y, startPosition.z),
                    Direction = new float3(direction.x, direction.y, direction.z),
                    Rotation = quaternion.identity,
                    Speed = Data.Speed,
                    MaxRange = Data.MaxRange,
                    HitRadius = 0.75f * math.max(Data.Scale, 0.01f),
                    ScaleMultiplier = math.max(Data.Scale, 0.01f),
                    CanPierce = Data.CanPierce ? (byte)1 : (byte)0,
                    TriggerDestroyEffectsOnMaxRange = Data.TriggerDestroyEffectsOnMaxRange ? (byte)1 : (byte)0,
                    Context = context.Clone(),
                    FlightTexture = Data.FlightTexture,
                    FlightFrameCount = math.clamp(Data.FlightFrameCount, 1, 16),
                    DestroyTexture = Data.DestroyTexture,
                    DestroyFrameCount = math.clamp(Data.DestroyFrameCount, 1, 16),
                    OnCollisionEffects = Data.OnCollisionEffects,
                    OnDestroyEffects = Data.OnDestroyEffects,
                });
        }
    }
}
