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
            });

        if (!EntityManager.HasBuffer<SkillProjectileHitEntityElement>(projectileEntity))
            EntityManager.AddBuffer<SkillProjectileHitEntityElement>(projectileEntity);
        else
            EntityManager.GetBuffer<SkillProjectileHitEntityElement>(projectileEntity).Clear();

        ApplyPayloadComponent(projectileEntity, request);
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
            existing.Context = payload.Context.Clone();
            existing.OnCollisionEffects = payload.OnCollisionEffects;
            existing.OnDestroyEffects = payload.OnDestroyEffects;
            return;
        }

        EntityManager.AddComponentObject(
            entity,
            new SkillProjectilePayloadComponent
            {
                Context = payload.Context.Clone(),
                OnCollisionEffects = payload.OnCollisionEffects,
                OnDestroyEffects = payload.OnDestroyEffects,
            });
    }
}
