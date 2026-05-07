using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 创建投射物效果，逻辑由投射物系统接入
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
            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, direction);
            Vector3 finalPosition = spawnPosition + direction * Data.SpawnOffsetDistance;

            GameObject projectile = PoolComponent.Instance.Get(Data.Projectile);
            projectile.transform.SetPositionAndRotation(finalPosition, rotation);
            projectile.transform.localScale = Data.Projectile.transform.localScale * Data.Scale;
            projectile.transform.right = direction;
            PrepareProjectileVisual(projectile);

            Flipbook4x4Runtime flipbook = projectile.GetComponent<Flipbook4x4Runtime>();
            if (flipbook == null)
                flipbook = projectile.AddComponent<Flipbook4x4Runtime>();

            flipbook.Initialize(loop: true, destroyWhenFinished: false);
            CreateProjectileEntity(context, projectile, finalPosition, direction);
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

        private void CreateProjectileEntity(SkillContent context, GameObject visual, Vector3 startPosition, Vector3 direction)
        {
            EntityManager entityManager = context.EntityManager;
            Entity projectileEntity = entityManager.CreateEntity(typeof(LocalTransform), typeof(SkillProjectileComponent));
            int registryId = SkillProjectileRegistry.Register(visual, context, Data.OnCollisionEffects, Data.OnDestroyEffects);

            entityManager.SetComponentData(
                projectileEntity,
                LocalTransform.FromPositionRotationScale(
                    startPosition,
                    quaternion.identity,
                    1f));

            entityManager.SetComponentData(
                projectileEntity,
                new SkillProjectileComponent
                {
                    Direction = new float3(direction.x, direction.y, direction.z),
                    Speed = Data.Speed,
                    MaxRange = Data.MaxRange,
                    TraveledDistance = 0f,
                    HitRadius = 0.75f,
                    RegistryId = registryId,
                    CanPierce = Data.CanPierce ? (byte)1 : (byte)0,
                    TriggerDestroyEffectsOnMaxRange = Data.TriggerDestroyEffectsOnMaxRange ? (byte)1 : (byte)0,
                });
        }

        private static void PrepareProjectileVisual(GameObject projectile)
        {
            SkillProjectileRuntime legacyRuntime = projectile.GetComponent<SkillProjectileRuntime>();
            if (legacyRuntime != null)
                legacyRuntime.enabled = false;

            Collider[] colliders = projectile.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Collider2D[] colliders2D = projectile.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
                colliders2D[i].enabled = false;
        }
    }
}
