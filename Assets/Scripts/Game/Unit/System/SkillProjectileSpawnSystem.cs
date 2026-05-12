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
    private EntityQuery _requestQuery;

    protected override void OnCreate()
    {
        _requestQuery = GetEntityQuery(
            ComponentType.ReadOnly<SkillProjectileSpawnRequestComponent>(),
            ComponentType.ReadOnly<SkillProjectilePayloadComponent>());
    }

    protected override void OnUpdate()
    {
        if (_requestQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> requestEntities = _requestQuery.ToEntityArray(Allocator.Temp);
        NativeArray<SkillProjectileSpawnRequestComponent> requests = _requestQuery.ToComponentDataArray<SkillProjectileSpawnRequestComponent>(Allocator.Temp);

        try
        {
            for (int i = 0; i < requestEntities.Length; i++)
            {
                Entity requestEntity = requestEntities[i];
                SkillProjectileSpawnRequestComponent request = requests[i];
                SkillProjectilePayloadComponent payload = EntityManager.GetComponentObject<SkillProjectilePayloadComponent>(requestEntity);

                if (!EntitySpawnRegistryUtility.TryInstantiateProjectile(EntityManager, request.ProjectileName, out Entity projectileEntity))
                {
                    Debug.LogError($"[SkillProjectileSpawnSystem] Missing projectile prefab in registry: {request.ProjectileName}");
                    if (EntityManager.Exists(requestEntity))
                        EntityManager.DestroyEntity(requestEntity);
                    continue;
                }

                SpawnProjectile(projectileEntity, request, payload);

                if (EntityManager.Exists(requestEntity))
                    EntityManager.DestroyEntity(requestEntity);
            }
        }
        finally
        {
            requestEntities.Dispose();
            requests.Dispose();
        }
    }

    private void SpawnProjectile(
        Entity projectileEntity,
        SkillProjectileSpawnRequestComponent request,
        SkillProjectilePayloadComponent payload)
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

        ApplyPayloadComponent(projectileEntity, payload);
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

    private void ApplyPayloadComponent(Entity entity, SkillProjectilePayloadComponent payload)
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
