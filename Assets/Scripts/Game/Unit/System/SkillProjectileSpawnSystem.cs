using System.Collections.Generic;
using CrystalMagic.Game.Unit;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[RunInGameWorld(GameWorldKind.Dungeon)]
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
        SpawnProjectileVisual(projectileEntity, request, rotation);
    }

    private void SpawnProjectileVisual(Entity projectileEntity, SkillProjectileSpawnRequest request, quaternion rotation)
    {
        if (request.VisualPrefabName.Length == 0 ||
            !SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                request.VisualPrefabName.ToString(),
                request.StartPosition,
                rotation,
                request.VisualScale,
                0f,
                out Entity visualEntity))
        {
            return;
        }

        SpriteEffectSpawnUtility.SetOrAddComponentData(
            EntityManager,
            visualEntity,
            new EffectVisualFollowComponent
            {
                Target = projectileEntity,
                Offset = request.VisualOffset,
                AlignRotation = 1,
                EndWhenTargetMissing = 1,
            });
        SetOrAddComponentData(projectileEntity, new SkillProjectileVisualLinkComponent { VisualEntity = visualEntity });
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
            existing.Context = CloneContext(payload.Context);
            existing.CollisionTargetConditions = CloneCollisionTargetConditions(payload.CollisionTargetConditions);
            existing.OnCollisionEffects = payload.OnCollisionEffects;
            existing.OnDestroyEffects = payload.OnDestroyEffects;
            return;
        }

        EntityManager.AddComponentObject(
            entity,
            new SkillProjectilePayloadComponent
            {
                Context = CloneContext(payload.Context),
                CollisionTargetConditions = CloneCollisionTargetConditions(payload.CollisionTargetConditions),
                OnCollisionEffects = payload.OnCollisionEffects,
                OnDestroyEffects = payload.OnDestroyEffects,
            });
        }

    private SkillContent CloneContext(SkillContent context)
    {
        SkillContent copy = context?.Clone() ?? new SkillContent();
        copy.EntityManager = EntityManager;
        return copy;
    }

    private static List<ConditionConfig> CloneCollisionTargetConditions(List<ConditionConfig> conditions)
    {
        return conditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(conditions);
    }
}
