using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSpawnSystem))]
public partial class SkillProjectileSystem : SystemBase
{
    private EntityQuery _projectileQuery;
    private readonly List<UnitQueryHit> _hits = new();

    protected override void OnCreate()
    {
        _projectileQuery = GetEntityQuery(
            ComponentType.ReadWrite<SkillProjectileComponent>(),
            ComponentType.ReadWrite<LocalTransform>(),
            ComponentType.ReadOnly<SkillProjectilePayloadComponent>(),
            ComponentType.ReadWrite<SkillProjectileHitEntityElement>());
    }

    protected override void OnUpdate()
    {
        if (_projectileQuery.IsEmptyIgnoreFilter ||
            !UnitQueryUtility.TryGetGrid(EntityManager, UnitQueryGridKind.Unit, out UnitQueryGrid unitGrid))
        {
            return;
        }

        float deltaTime = SystemAPI.Time.DeltaTime;
        Dependency = new ProjectileMoveJob
        {
            DeltaTime = deltaTime,
        }.ScheduleParallel(Dependency);
        Dependency.Complete();

        NativeArray<Entity> entities = _projectileQuery.ToEntityArray(Allocator.Temp);
        NativeArray<SkillProjectileComponent> projectiles = _projectileQuery.ToComponentDataArray<SkillProjectileComponent>(Allocator.Temp);
        NativeArray<LocalTransform> transforms = _projectileQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        try
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                SkillProjectileComponent projectile = projectiles[i];
                LocalTransform transform = transforms[i];
                SkillProjectilePayloadComponent payload = EntityManager.GetComponentData<SkillProjectilePayloadComponent>(entity);
                DynamicBuffer<SkillProjectileHitEntityElement> hitEntities = EntityManager.GetBuffer<SkillProjectileHitEntityElement>(entity);
                SkillContent baseContext = EffectUtility.CreateContext(EntityManager, in payload.Context);
                EffectDataBridgeUtility.TryGetConditions(
                    EntityManager,
                    payload.CollisionTargetConditionsId,
                    out List<ConditionConfig> collisionTargetConditions);

                if (TryFindHitEntity(
                        unitGrid,
                        in payload,
                        baseContext,
                        collisionTargetConditions,
                        projectile,
                        hitEntities,
                        transform.Position,
                        out Entity hitEntity,
                        out float3 hitPosition))
                {
                    hitEntities.Add(new SkillProjectileHitEntityElement { Value = hitEntity });

                    SkillContent hitContext = BuildHitContext(baseContext, hitEntity, hitPosition);
                    EffectUtility.Enqueue(EntityManager, payload.OnCollisionEffectListId, hitContext);

                    if (projectile.CanPierce == 0)
                    {
                        DestroyProjectile(entity, payload, transform.Position, true, hitContext);
                        continue;
                    }
                }

                if (projectile.MaxRange > 0f && projectile.TraveledDistance >= projectile.MaxRange)
                {
                    DestroyProjectile(
                        entity,
                        payload,
                        transform.Position,
                        projectile.TriggerDestroyEffectsOnMaxRange != 0,
                        null);
                }
            }
        }
        finally
        {
            entities.Dispose();
            projectiles.Dispose();
            transforms.Dispose();
        }
    }

    private bool TryFindHitEntity(
        UnitQueryGrid unitGrid,
        in SkillProjectilePayloadComponent payload,
        SkillContent baseContext,
        IReadOnlyList<ConditionConfig> collisionTargetConditions,
        SkillProjectileComponent projectile,
        DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        float3 projectilePosition,
        out Entity hitEntity,
        out float3 hitPosition)
    {
        hitEntity = Entity.Null;
        hitPosition = float3.zero;

        unitGrid.QueryCircle(projectilePosition, projectile.HitRadius, _hits);

        float bestDistanceSq = float.MaxValue;
        for (int i = 0; i < _hits.Count; i++)
        {
            UnitQueryHit hit = _hits[i];
            if (payload.Context.HasOriginEntity != 0 && hit.Entity == payload.Context.OriginEntity)
                continue;

            if (HasHitEntity(hitEntities, hit.Entity))
                continue;

            if (!EffectConditionUtility.Pass(collisionTargetConditions, baseContext, hit.Entity))
                continue;

            float distanceSq = math.lengthsq(hit.Position.xy - projectilePosition.xy);
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            hitEntity = hit.Entity;
            hitPosition = new float3(hit.Position.x, hit.Position.y, projectilePosition.z);
        }

        return hitEntity != Entity.Null;
    }

    private static bool HasHitEntity(DynamicBuffer<SkillProjectileHitEntityElement> hitEntities, Entity entity)
    {
        for (int i = 0; i < hitEntities.Length; i++)
        {
            if (hitEntities[i].Value == entity)
                return true;
        }

        return false;
    }

    private void DestroyProjectile(
        Entity entity,
        SkillProjectilePayloadComponent payload,
        float3 destroyPosition,
        bool triggerDestroyEffects,
        SkillContent destroyContext)
    {
        if (triggerDestroyEffects)
        {
            SkillContent context = destroyContext?.Clone() ??
                                   EffectUtility.CreateContext(EntityManager, in payload.Context);
            context.EntityManager = EntityManager;
            context.HasPosition = true;
            context.Position = new UnityEngine.Vector3(destroyPosition.x, destroyPosition.y, destroyPosition.z);
            EffectUtility.Enqueue(EntityManager, payload.OnDestroyEffectListId, context);
        }

        EffectUtility.ReleaseAfterExecution(EntityManager, payload.OnCollisionEffectListId);
        EffectUtility.ReleaseAfterExecution(EntityManager, payload.OnDestroyEffectListId);
        EffectDataBridgeUtility.UnregisterConditions(
            EntityManager,
            payload.CollisionTargetConditionsId);
        if (payload.OwnsManagedContext != 0)
            EffectDataBridgeUtility.UnregisterManagedContext(EntityManager, payload.Context.ManagedContextId);
        payload.OnCollisionEffectListId = default;
        payload.OnDestroyEffectListId = default;
        payload.CollisionTargetConditionsId = default;
        payload.OwnsManagedContext = 0;
        EntityManager.SetComponentData(entity, payload);

        if (!EntityManager.Exists(entity))
            return;

        if (EntityManager.HasComponent<SkillProjectileVisualLinkComponent>(entity))
        {
            Entity visualEntity = EntityManager.GetComponentData<SkillProjectileVisualLinkComponent>(entity).VisualEntity;
            SpriteEffectAnimationSystem.RequestEnd(EntityManager, visualEntity);
        }

        if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
            EntityManager.AddComponent<DestroyEntityFlag>(entity);

        EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
    }

    private SkillContent BuildHitContext(SkillContent baseContext, Entity hitEntity, float3 hitPosition)
    {
        SkillContent context = baseContext.Clone();
        context.EntityManager = EntityManager;
        context.HasPosition = true;
        context.Position = new UnityEngine.Vector3(hitPosition.x, hitPosition.y, hitPosition.z);
        context.HasTargetEntity = true;
        context.TargetEntity = hitEntity;
        context.HasTarget = false;
        context.Target = null;
        return context;
    }

    [BurstCompile]
    private partial struct ProjectileMoveJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref SkillProjectileComponent projectile, ref LocalTransform transform)
        {
            float moveDistance = projectile.Speed * DeltaTime;
            transform.Position += projectile.Direction * moveDistance;
            float2 planar = math.normalizesafe(projectile.Direction.xy, new float2(1f, 0f));
            transform.Rotation = quaternion.RotateZ(math.atan2(planar.y, planar.x));
            projectile.TraveledDistance += math.abs(moveDistance);
            projectile.NetworkDirty = 1;
        }
    }
}
