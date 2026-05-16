using Unity.Collections;
using Unity.Entities;
using CrystalMagic.Game.Unit;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillExecutionSystem))]
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

            if (request.Kind == SkillProjectileSpawnRequestKind.DestroyVfx)
            {
                SpawnDestroyVfx(projectileEntity, request);
            }
            else
            {
                SpawnProjectile(projectileEntity, request);
            }
        }
    }

    private void SpawnProjectile(
        Entity projectileEntity,
        SkillProjectileSpawnRequest request)
    {
        quaternion rotation = CreateRotation(request.Direction);
        float prefabScale = 1f;
        if (EntityManager.HasComponent<LocalTransform>(projectileEntity))
            prefabScale = math.max(0.0001f, EntityManager.GetComponentData<LocalTransform>(projectileEntity).Scale);

        float scale = math.max(0.0001f, prefabScale * request.ScaleMultiplier);

        SetOrAddComponentData(
            projectileEntity,
            LocalTransform.FromPositionRotationScale(
                request.StartPosition,
                rotation,
                scale));

        SetOrAddComponentData(
            projectileEntity,
            new SkillProjectileComponent
            {
                Direction = math.normalizesafe(request.Direction, new float3(1f, 0f, 0f)),
                Speed = request.Speed,
                MaxRange = request.MaxRange,
                TraveledDistance = 0f,
                HitRadius = request.HitRadius,
                Scale = scale,
                CanPierce = request.CanPierce,
                TriggerDestroyEffectsOnMaxRange = request.TriggerDestroyEffectsOnMaxRange,
            });

        SetOrAddComponentData(
            projectileEntity,
            new SkillProjectileStartTimeProperty
            {
                Value = (float)SystemAPI.Time.ElapsedTime,
            });

        if (!EntityManager.HasBuffer<SkillProjectileHitEntityElement>(projectileEntity))
            EntityManager.AddBuffer<SkillProjectileHitEntityElement>(projectileEntity);
        else
            EntityManager.GetBuffer<SkillProjectileHitEntityElement>(projectileEntity).Clear();

        EnsureDestroyFlagDisabled(projectileEntity);

        ApplyPayloadComponent(projectileEntity, request);
        ProjectileVisualUtility.ApplyProjectileVisual(
            EntityManager,
            projectileEntity,
            request.ProjectileName,
            request.FlightTexture,
            true,
            request.FlightFrameCount);
    }

    private void SpawnDestroyVfx(Entity projectileEntity, SkillProjectileSpawnRequest request)
    {
        float scale = math.max(request.ScaleMultiplier, 0.0001f);
        SetOrAddComponentData(
            projectileEntity,
            LocalTransform.FromPositionRotationScale(
                request.StartPosition,
                request.Rotation,
                scale));

        SetOrAddComponentData(
            projectileEntity,
            new SkillProjectileStartTimeProperty
            {
                Value = (float)SystemAPI.Time.ElapsedTime,
            });

        SetOrAddComponentData(
            projectileEntity,
            new ProjectileDestroyVfxComponent
            {
                RemainingLifetime = ProjectileVisualUtility.GetAnimationLifetime(request.ProjectileName, request.DestroyFrameCount),
            });

        EnsureDestroyFlagDisabled(projectileEntity);
        ProjectileVisualUtility.ApplyProjectileVisual(
            EntityManager,
            projectileEntity,
            request.ProjectileName,
            request.DestroyTexture,
            false,
            request.DestroyFrameCount);
    }

    private void EnsureDestroyFlagDisabled(Entity entity)
    {
        if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
            EntityManager.AddComponent<DestroyEntityFlag>(entity);

        EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, false);
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
            existing.FlightFrameCount = payload.FlightFrameCount;
            existing.DestroyTexture = payload.DestroyTexture;
            existing.DestroyFrameCount = payload.DestroyFrameCount;
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
                FlightFrameCount = payload.FlightFrameCount,
                DestroyTexture = payload.DestroyTexture,
                DestroyFrameCount = payload.DestroyFrameCount,
                OnCollisionEffects = payload.OnCollisionEffects,
                OnDestroyEffects = payload.OnDestroyEffects,
            });
    }
}
