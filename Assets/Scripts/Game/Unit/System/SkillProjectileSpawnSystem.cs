using CrystalMagic.Game.Unit;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
[UpdateBefore(typeof(SkillProjectileSystem))]
public partial class SkillProjectileSpawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SkillProjectileSpawnQueue.HasPendingRequests)
            return;

        while (SkillProjectileSpawnQueue.TryDequeue(out SkillProjectileSpawnRequest request))
        {
            if (!EntitySpawnRegistryUtility.TryInstantiateProjectile(EntityManager, request.ProjectileName, out Entity projectileEntity))
            {
                Debug.LogError($"[SkillProjectileSpawnSystem] Missing projectile prefab in registry: {request.ProjectileName}");
                continue;
            }

            SpawnProjectile(projectileEntity, request);
        }
    }

    private void SpawnProjectile(Entity projectileEntity, SkillProjectileSpawnRequest request)
    {
        quaternion rotation = CreateRotation(request.Direction);

        SetOrAddComponentData( projectileEntity, LocalTransform.FromPositionRotationScale(request.StartPosition,rotation,1f));

        SetOrAddComponentData(projectileEntity,new SkillProjectileComponent
            {
                Direction = math.normalizesafe(request.Direction, new float3(1f, 0f, 0f)),
                Speed = request.Speed,
                MaxRange = request.MaxRange,
                TraveledDistance = 0f,
                HitRadius = request.HitRadius,
                CanPierce = request.CanPierce,
                TriggerDestroyEffectsOnMaxRange = request.TriggerDestroyEffectsOnMaxRange,
                IsDestroying = 0,
            });

        if (!EntityManager.HasBuffer<SkillProjectileHitEntityElement>(projectileEntity))
            EntityManager.AddBuffer<SkillProjectileHitEntityElement>(projectileEntity);
        else
            EntityManager.GetBuffer<SkillProjectileHitEntityElement>(projectileEntity).Clear();

        EnsureAnimationPropertyComponents(projectileEntity);
        ApplyPayloadComponent(projectileEntity, request);
        ApplyAnimation(
            projectileEntity,
            QuadAnimationVisualKind.Projectile,
            request.ProjectileName.ToString(),
            request.FlightTexture,
            request.FlightGridColumns,
            request.FlightGridRows,
            request.FlightFrameCount,
            request.FlightFramesPerSecond,
            request.Width,
            request.Height,
            loop: true,
            autoDestroyOnComplete: false,
            lifetimeSeconds: 0f);
    }

    private void EnsureAnimationPropertyComponents(Entity entity)
    {
        SetOrAddComponentData(entity, new UnitAnimationFrameUvMinProperty { Value = new float4(0f, 0f, 0f, 0f) });
        SetOrAddComponentData(entity, new UnitAnimationFrameUvSizeProperty { Value = new float4(1f, 1f, 0f, 0f) });
        SetOrAddComponentData(entity, new UnitAnimationFrameWorldSizeProperty { Value = new float4(1f, 1f, 0f, 0f) });
        SetOrAddComponentData(entity, new UnitAnimationFramePivotOffsetProperty { Value = new float4(0f, 0f, 0f, 0f) });
    }

    private void ApplyAnimation(
        Entity entity,
        QuadAnimationVisualKind visualKind,
        string prefabName,
        Texture2D texture,
        int gridColumns,
        int gridRows,
        int frameCount,
        float fps,
        float width,
        float height,
        bool loop,
        bool autoDestroyOnComplete,
        float lifetimeSeconds)
    {
        SetOrAddComponentData(
            entity,
            new QuadAnimationComponent
            {
                GridColumns = math.max(1, gridColumns),
                GridRows = math.max(1, gridRows),
                FrameCount = math.max(1, frameCount),
                FramesPerSecond = math.max(0.01f, fps),
                ElapsedSeconds = 0f,
                Width = math.max(0.01f, width),
                Height = math.max(0.01f, height),
                PivotOffset = float2.zero,
                RemainingLifetimeSeconds = math.max(0f, lifetimeSeconds),
                FrameIndex = -1,
                LastTextureInstanceId = 0,
                LastVisualKeyHash = 0,
                Loop = loop ? (byte)1 : (byte)0,
                AutoDestroyOnComplete = autoDestroyOnComplete ? (byte)1 : (byte)0,
                IsPlaying = 1,
            });

        if (EntityManager.HasComponent<QuadAnimationVisualComponent>(entity))
        {
            QuadAnimationVisualComponent visual = EntityManager.GetComponentObject<QuadAnimationVisualComponent>(entity);
            visual.VisualKind = visualKind;
            visual.PrefabName = prefabName;
            visual.Texture = texture;
        }
        else
        {
            EntityManager.AddComponentObject(
                entity,
                new QuadAnimationVisualComponent
                {
                    VisualKind = visualKind,
                    PrefabName = prefabName,
                    Texture = texture,
                });
        }
    }

    private static quaternion CreateRotation(float3 direction)
    {
        float2 planar = math.normalizesafe(direction.xy, new float2(1f, 0f));
        float angle = math.atan2(planar.y, planar.x);
        return quaternion.RotateZ(angle);
    }

    private void SetOrAddComponentData<T>(Entity entity, T value)
        where T : unmanaged, IComponentData
    {
        if (EntityManager.HasComponent<T>(entity))
            EntityManager.SetComponentData(entity, value);
        else
            EntityManager.AddComponentData(entity, value);
    }

    private void ApplyPayloadComponent(Entity entity, SkillProjectileSpawnRequest payload)
    {
        if (EntityManager.HasComponent<SkillProjectilePayloadComponent>(entity))
        {
            SkillProjectilePayloadComponent existing = EntityManager.GetComponentObject<SkillProjectilePayloadComponent>(entity);
            existing.ProjectileName = payload.ProjectileName;
            existing.Context = payload.Context.Clone();
            existing.FlightTexture = payload.FlightTexture;
            existing.FlightGridColumns = payload.FlightGridColumns;
            existing.FlightGridRows = payload.FlightGridRows;
            existing.FlightFrameCount = payload.FlightFrameCount;
            existing.FlightFramesPerSecond = payload.FlightFramesPerSecond;
            existing.FlightWidth = payload.Width;
            existing.FlightHeight = payload.Height;
            existing.DestroyTexture = payload.DestroyTexture;
            existing.DestroyGridColumns = payload.DestroyGridColumns;
            existing.DestroyGridRows = payload.DestroyGridRows;
            existing.DestroyFrameCount = payload.DestroyFrameCount;
            existing.DestroyFramesPerSecond = payload.DestroyFramesPerSecond;
            existing.DestroyWidth = payload.Width;
            existing.DestroyHeight = payload.Height;
            existing.OnCollisionEffects = payload.OnCollisionEffects;
            existing.OnDestroyEffects = payload.OnDestroyEffects;
            return;
        }

        EntityManager.AddComponentObject(
            entity,
            new SkillProjectilePayloadComponent
            {
                ProjectileName = payload.ProjectileName,
                Context = payload.Context.Clone(),
                FlightTexture = payload.FlightTexture,
                FlightGridColumns = payload.FlightGridColumns,
                FlightGridRows = payload.FlightGridRows,
                FlightFrameCount = payload.FlightFrameCount,
                FlightFramesPerSecond = payload.FlightFramesPerSecond,
                FlightWidth = payload.Width,
                FlightHeight = payload.Height,
                DestroyTexture = payload.DestroyTexture,
                DestroyGridColumns = payload.DestroyGridColumns,
                DestroyGridRows = payload.DestroyGridRows,
                DestroyFrameCount = payload.DestroyFrameCount,
                DestroyFramesPerSecond = payload.DestroyFramesPerSecond,
                DestroyWidth = payload.Width,
                DestroyHeight = payload.Height,
                OnCollisionEffects = payload.OnCollisionEffects,
                OnDestroyEffects = payload.OnDestroyEffects,
            });
    }
}
