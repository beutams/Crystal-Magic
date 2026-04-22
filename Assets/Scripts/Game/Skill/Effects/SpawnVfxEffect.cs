using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 生成特效效果，逻辑由特效系统接入
    /// </summary>
    public sealed class SpawnVfxEffect : Effect
    {
        public new SpawnVfxEffectData Data { get; }

        public SpawnVfxEffect(SpawnVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            GameObject prefab = ResourceComponent.Instance.Load<GameObject>(Data.VfxPath);
            if (prefab == null)
                return;

            Quaternion rotation = GetSpawnRotation(context);
            Vector3 position = GetSpawnPosition(context, rotation);
            GameObject vfx = Object.Instantiate(prefab, position, rotation);
            vfx.transform.localScale *= Data.Scale;

            if (Data.FollowCaster && context.HasOriginEntity)
            {
                SkillVfxRuntime runtime = vfx.GetComponent<SkillVfxRuntime>();
                if (runtime == null)
                    runtime = vfx.AddComponent<SkillVfxRuntime>();

                runtime.Initialize(context.OriginEntity, context.EntityManager, Data.SpawnOffset, Data.AlignToCasterForward, Data.Duration);
                return;
            }

            if (Data.Duration > 0f)
                Object.Destroy(vfx, Data.Duration);
        }

        private Vector3 GetSpawnPosition(SkillContent context, Quaternion rotation)
        {
            Vector3 basePosition = TryGetReleasePosition(context, out Vector3 releasePosition)
                ? releasePosition
                : Vector3.zero;

            return basePosition + rotation * Data.SpawnOffset;
        }

        private Quaternion GetSpawnRotation(SkillContent context)
        {
            if (!Data.AlignToCasterForward)
                return Quaternion.identity;

            if (TryGetEntityRotation(context.HasOriginEntity, context.OriginEntity, context.EntityManager, out Quaternion rotation))
                return rotation;

            return Quaternion.identity;
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

        private static bool TryGetEntityRotation(bool hasEntity, Entity entity, EntityManager entityManager, out Quaternion rotation)
        {
            if (hasEntity &&
                entity != Entity.Null &&
                entityManager.Exists(entity) &&
                entityManager.HasComponent<LocalTransform>(entity))
            {
                Unity.Mathematics.quaternion entityRotation = entityManager.GetComponentData<LocalTransform>(entity).Rotation;
                rotation = new Quaternion(entityRotation.value.x, entityRotation.value.y, entityRotation.value.z, entityRotation.value.w);
                return true;
            }

            rotation = Quaternion.identity;
            return false;
        }
    }

    public sealed class SkillVfxRuntime : MonoBehaviour
    {
        private Entity _followEntity;
        private EntityManager _entityManager;
        private Vector3 _offset;
        private bool _alignToEntityRotation;
        private float _destroyTime;

        public void Initialize(Entity followEntity, EntityManager entityManager, Vector3 offset, bool alignToEntityRotation, float duration)
        {
            _followEntity = followEntity;
            _entityManager = entityManager;
            _offset = offset;
            _alignToEntityRotation = alignToEntityRotation;
            _destroyTime = duration > 0f ? Time.time + duration : 0f;
            RefreshTransform();
        }

        private void Update()
        {
            RefreshTransform();

            if (_destroyTime > 0f && Time.time >= _destroyTime)
                Destroy(gameObject);
        }

        private void RefreshTransform()
        {
            if (_followEntity == Entity.Null ||
                !_entityManager.Exists(_followEntity) ||
                !_entityManager.HasComponent<LocalTransform>(_followEntity))
                return;

            LocalTransform followTransform = _entityManager.GetComponentData<LocalTransform>(_followEntity);
            Quaternion rotation = Quaternion.identity;
            if (_alignToEntityRotation)
            {
                Unity.Mathematics.quaternion entityRotation = followTransform.Rotation;
                rotation = new Quaternion(entityRotation.value.x, entityRotation.value.y, entityRotation.value.z, entityRotation.value.w);
                transform.rotation = rotation;
            }

            Unity.Mathematics.float3 entityPosition = followTransform.Position;
            transform.position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z) + rotation * _offset;
        }
    }
}
