using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
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
        if (_projectileQuery.IsEmptyIgnoreFilter || !SystemAPI.HasSingleton<UnitQuerySingleton>())
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        DynamicBuffer<UnitQueryEntry> queryEntries = SystemAPI.GetSingletonBuffer<UnitQueryEntry>(true);

        NativeArray<Entity> entities = _projectileQuery.ToEntityArray(Allocator.Temp);
        NativeArray<SkillProjectileComponent> projectiles = _projectileQuery.ToComponentDataArray<SkillProjectileComponent>(Allocator.Temp);
        NativeArray<LocalTransform> transforms = _projectileQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        try
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                SkillProjectileComponent projectile = projectiles[i];
                if (projectile.IsDestroying != 0)
                    continue;

                LocalTransform transform = transforms[i];
                SkillProjectilePayloadComponent payload = EntityManager.GetComponentObject<SkillProjectilePayloadComponent>(entity);
                DynamicBuffer<SkillProjectileHitEntityElement> hitEntities = EntityManager.GetBuffer<SkillProjectileHitEntityElement>(entity);

                float moveDistance = projectile.Speed * deltaTime;
                transform.Position += projectile.Direction * moveDistance;
                transform.Rotation = CreateRotation(projectile.Direction);
                projectile.TraveledDistance += math.abs(moveDistance);

                EntityManager.SetComponentData(entity, transform);
                EntityManager.SetComponentData(entity, projectile);

                if (TryFindHitEntity(queryEntries, payload, projectile, hitEntities, transform.Position, out Entity hitEntity, out float3 hitPosition))
                {
                    hitEntities.Add(new SkillProjectileHitEntityElement { Value = hitEntity });

                    SkillContent hitContext = BuildHitContext(payload.Context, hitEntity, hitPosition);
                    SkillExecutor.ExecuteEffects(payload.OnCollisionEffects, hitContext);

                    if (projectile.CanPierce == 0)
                    {
                        DestroyProjectile(entity, payload, transform.Position, transform.Rotation, true, hitContext);
                        continue;
                    }
                }

                if (projectile.MaxRange > 0f && projectile.TraveledDistance >= projectile.MaxRange)
                {
                    DestroyProjectile(
                        entity,
                        payload,
                        transform.Position,
                        transform.Rotation,
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
        DynamicBuffer<UnitQueryEntry> queryEntries,
        SkillProjectilePayloadComponent payload,
        SkillProjectileComponent projectile,
        DynamicBuffer<SkillProjectileHitEntityElement> hitEntities,
        float3 projectilePosition,
        out Entity hitEntity,
        out float3 hitPosition)
    {
        hitEntity = Entity.Null;
        hitPosition = float3.zero;

        UnitQueryUtility.QueryCircle(queryEntries, projectilePosition, projectile.HitRadius, _hits);

        float bestDistanceSq = float.MaxValue;
        for (int i = 0; i < _hits.Count; i++)
        {
            UnitQueryHit hit = _hits[i];
            if (payload.Context.HasOriginEntity && hit.Entity == payload.Context.OriginEntity)
                continue;

            if (HasHitEntity(hitEntities, hit.Entity))
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
        quaternion destroyRotation,
        bool triggerDestroyEffects,
        SkillContent destroyContext)
    {
        if (triggerDestroyEffects)
        {
            SkillContent context = destroyContext?.Clone() ?? payload.Context.Clone();
            context.EntityManager = EntityManager;
            context.HasPosition = true;
            context.Position = new UnityEngine.Vector3(destroyPosition.x, destroyPosition.y, destroyPosition.z);
            SkillExecutor.ExecuteEffects(payload.OnDestroyEffects, context);
        }

        if (!EntityManager.Exists(entity))
            return;

        if (payload.DestroyTexture == null || payload.DestroyFrameCount <= 0)
        {
            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            return;
        }

        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
        transform.Position = destroyPosition;
        transform.Rotation = destroyRotation;
        EntityManager.SetComponentData(entity, transform);

        SkillProjectileComponent projectile = EntityManager.GetComponentData<SkillProjectileComponent>(entity);
        projectile.IsDestroying = 1;
        projectile.Speed = 0f;
        EntityManager.SetComponentData(entity, projectile);

        EntityManager.SetComponentData(
            entity,
            new QuadAnimationComponent
            {
                GridColumns = math.max(1, payload.DestroyGridColumns),
                GridRows = math.max(1, payload.DestroyGridRows),
                FrameCount = math.max(1, payload.DestroyFrameCount),
                FramesPerSecond = math.max(0.01f, payload.DestroyFramesPerSecond),
                ElapsedSeconds = 0f,
                Width = math.max(0.01f, payload.DestroyWidth),
                Height = math.max(0.01f, payload.DestroyHeight),
                PivotOffset = float2.zero,
                RemainingLifetimeSeconds = 0f,
                FrameIndex = -1,
                LastTextureInstanceId = 0,
                LastVisualKeyHash = 0,
                Loop = 0,
                AutoDestroyOnComplete = 1,
                IsPlaying = 1,
            });

        if (EntityManager.HasComponent<QuadAnimationVisualComponent>(entity))
        {
            QuadAnimationVisualComponent visual = EntityManager.GetComponentObject<QuadAnimationVisualComponent>(entity);
            visual.VisualKind = QuadAnimationVisualKind.Projectile;
            visual.PrefabName = payload.ProjectileName.ToString();
            visual.Texture = payload.DestroyTexture;
        }
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

    private static quaternion CreateRotation(float3 direction)
    {
        float2 planar = math.normalizesafe(direction.xy, new float2(1f, 0f));
        float angle = math.atan2(planar.y, planar.x);
        return quaternion.RotateZ(angle);
    }
}
