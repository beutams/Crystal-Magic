using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 生成特效效果，由通用 Quad 动画系统负责播放与销毁。
    /// </summary>
    public sealed class SpawnVfxEffect : Effect
    {
        public new SpawnVfxEffectData Data { get; }

        public SpawnVfxEffect(SpawnVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null)
                return;

            EntityManager entityManager = context.EntityManager;
            if (!EntitySpawnRegistryUtility.TryInstantiateVfx(entityManager, new FixedString128Bytes(QuadAnimationVisualUtility.GenericVfxPrefabName), out Entity vfxEntity))
                return;

            Quaternion rotation = GetSpawnRotation(context);
            Vector3 position = GetSpawnPosition(context, rotation);
            SetOrAddComponentData(
                entityManager,
                vfxEntity,
                LocalTransform.FromPositionRotationScale(
                    new float3(position.x, position.y, position.z),
                    new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                    1f));

            EnsureAnimationPropertyComponents(entityManager, vfxEntity);
            ConfigureAnimation(entityManager, vfxEntity);
            ConfigureFollow(entityManager, vfxEntity, context);
        }

        private void ConfigureAnimation(EntityManager entityManager, Entity entity)
        {
            SetOrAddComponentData(
                entityManager,
                entity,
                new QuadAnimationComponent
                {
                    GridColumns = math.max(1, Data.GridColumns),
                    GridRows = math.max(1, Data.GridRows),
                    FrameCount = math.max(1, Data.FrameCount),
                    FramesPerSecond = math.max(0.01f, Data.FramesPerSecond),
                    ElapsedSeconds = 0f,
                    Width = math.max(0.01f, Data.Width > 0f ? Data.Width : Data.Scale),
                    Height = math.max(0.01f, Data.Height > 0f ? Data.Height : Data.Scale),
                    PivotOffset = float2.zero,
                    RemainingLifetimeSeconds = Data.Loop ? math.max(0f, Data.Duration) : 0f,
                    FrameIndex = -1,
                    LastTextureInstanceId = 0,
                    LastVisualKeyHash = 0,
                    Loop = Data.Loop ? (byte)1 : (byte)0,
                    AutoDestroyOnComplete = 1,
                    IsPlaying = 1,
                });

            if (entityManager.HasComponent<QuadAnimationVisualComponent>(entity))
            {
                QuadAnimationVisualComponent visual = entityManager.GetComponentObject<QuadAnimationVisualComponent>(entity);
                visual.PrefabName = QuadAnimationVisualUtility.GenericVfxPrefabName;
                visual.Texture = Data.VfxTexture;
            }
            else
            {
                entityManager.AddComponentObject(
                    entity,
                    new QuadAnimationVisualComponent
                    {
                        PrefabName = QuadAnimationVisualUtility.GenericVfxPrefabName,
                        Texture = Data.VfxTexture,
                    });
            }
        }

        private void ConfigureFollow(EntityManager entityManager, Entity entity, SkillContent context)
        {
            bool shouldFollow = Data.FollowCaster && context.HasOriginEntity;
            if (!shouldFollow)
            {
                if (entityManager.HasComponent<FollowEntityComponent>(entity))
                    entityManager.RemoveComponent<FollowEntityComponent>(entity);
                return;
            }

            FollowEntityComponent follow = new()
            {
                Target = context.OriginEntity,
                Offset = new float3(Data.SpawnOffset.x, Data.SpawnOffset.y, Data.SpawnOffset.z),
                AlignRotation = Data.AlignToCasterForward ? (byte)1 : (byte)0,
            };

            if (entityManager.HasComponent<FollowEntityComponent>(entity))
                entityManager.SetComponentData(entity, follow);
            else
                entityManager.AddComponentData(entity, follow);
        }

        private static void EnsureAnimationPropertyComponents(EntityManager entityManager, Entity entity)
        {
            SetOrAddComponentData(entityManager, entity, new QuadAnimationFrameUvMinProperty { Value = new float4(0f, 0f, 0f, 0f) });
            SetOrAddComponentData(entityManager, entity, new QuadAnimationFrameUvSizeProperty { Value = new float4(1f, 1f, 0f, 0f) });
            SetOrAddComponentData(entityManager, entity, new QuadAnimationFrameWorldSizeProperty { Value = new float4(1f, 1f, 0f, 0f) });
            SetOrAddComponentData(entityManager, entity, new QuadAnimationFramePivotOffsetProperty { Value = new float4(0f, 0f, 0f, 0f) });
        }

        private static void SetOrAddComponentData<T>(EntityManager entityManager, Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                entityManager.SetComponentData(entity, value);
            else
                entityManager.AddComponentData(entity, value);
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

            if (TryGetEntityFacingRotation(context.HasOriginEntity, context.OriginEntity, context.EntityManager, out Quaternion rotation))
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
                float3 entityPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;
                position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryGetEntityFacingRotation(bool hasEntity, Entity entity, EntityManager entityManager, out Quaternion rotation)
        {
            if (hasEntity &&
                entity != Entity.Null &&
                entityManager.Exists(entity) &&
                UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facing))
            {
                quaternion entityRotation = UnitFacingUtility.CreateRotation(facing);
                rotation = new Quaternion(entityRotation.value.x, entityRotation.value.y, entityRotation.value.z, entityRotation.value.w);
                return true;
            }

            rotation = Quaternion.identity;
            return false;
        }
    }
}
