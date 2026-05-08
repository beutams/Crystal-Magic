using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SkillProjectileSystem : SystemBase
    {
        private EntityQuery _projectileQuery;
        private readonly List<UnitQueryHit> _hits = new();

        protected override void OnCreate()
        {
            _projectileQuery = GetEntityQuery(
                ComponentType.ReadWrite<SkillProjectileComponent>(),
                ComponentType.ReadWrite<LocalTransform>());
        }

        protected override void OnUpdate()
        {
            if (_projectileQuery.IsEmptyIgnoreFilter)
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;
            if (!SystemAPI.HasSingleton<UnitQuerySingleton>())
                return;

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
                    LocalTransform transform = transforms[i];

                    if (!SkillProjectileRegistry.TryGet(projectile.RegistryId, out SkillProjectileRegistry.State state))
                    {
                        EntityManager.DestroyEntity(entity);
                        continue;
                    }

                    float moveDistance = projectile.Speed * deltaTime;
                    transform.Position += projectile.Direction * moveDistance;
                    projectile.TraveledDistance += math.abs(moveDistance);
                    EntityManager.SetComponentData(entity, transform);
                    EntityManager.SetComponentData(entity, projectile);

                    SyncVisual(state, transform.Position, projectile.Direction);

                    if (TryFindHitEntity(queryEntries, state, projectile, out Entity hitEntity, out float3 hitPosition))
                    {
                        SkillContent hitContext = BuildHitContext(state.Context, hitEntity, hitPosition);
                        SkillExecutor.ExecuteEffects(state.OnCollisionEffects, hitContext);

                        if (projectile.CanPierce == 0)
                        {
                            DestroyProjectile(entity, projectile, hitPosition, true, hitContext);
                            continue;
                        }
                    }

                    if (projectile.MaxRange > 0f && projectile.TraveledDistance >= projectile.MaxRange)
                    {
                        DestroyProjectile(
                            entity,
                            projectile,
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
            DynamicBuffer<UnitQueryEntry> queryEntries,
            SkillProjectileRegistry.State state,
            SkillProjectileComponent projectile,
            out Entity hitEntity,
            out float3 hitPosition)
        {
            hitEntity = Entity.Null;
            hitPosition = float3.zero;

            float3 projectilePosition = GetProjectilePosition(state);
            UnitQueryUtility.QueryCircle(queryEntries, projectilePosition, projectile.HitRadius, _hits);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (state.Context.HasOriginEntity && hit.Entity == state.Context.OriginEntity)
                    continue;

                if (!state.HitEntities.Add(hit.Entity))
                    continue;

                float distanceSq = math.lengthsq(hit.Position.xy - new float2(projectilePosition.x, projectilePosition.y));
                if (distanceSq >= bestDistanceSq)
                {
                    state.HitEntities.Remove(hit.Entity);
                    continue;
                }

                if (hitEntity != Entity.Null)
                    state.HitEntities.Remove(hitEntity);

                bestDistanceSq = distanceSq;
                hitEntity = hit.Entity;
                hitPosition = new float3(hit.Position.x, hit.Position.y, projectilePosition.z);
            }

            return hitEntity != Entity.Null;
        }

        private static void SyncVisual(SkillProjectileRegistry.State state, float3 position, float3 direction)
        {
            if (state.Visual == null)
                return;

            state.Visual.transform.position = new Vector3(position.x, position.y, position.z);
            Vector3 forward = new(direction.x, direction.y, direction.z);
            if (forward.sqrMagnitude > 0.0001f)
                state.Visual.transform.right = forward;
        }

        private void DestroyProjectile(
            Entity entity,
            SkillProjectileComponent projectile,
            float3 destroyPosition,
            bool triggerDestroyEffects,
            SkillContent destroyContext)
        {
            if (SkillProjectileRegistry.TryRemove(projectile.RegistryId, out SkillProjectileRegistry.State state))
            {
                if (triggerDestroyEffects)
                {
                    SkillContent context = destroyContext?.Clone() ?? state.Context.Clone();
                    context.EntityManager = EntityManager;
                    context.HasPosition = true;
                    context.Position = new Vector3(destroyPosition.x, destroyPosition.y, destroyPosition.z);
                    SkillExecutor.ExecuteEffects(state.OnDestroyEffects, context);
                }

                if (state.Visual != null)
                    PoolComponent.Instance.Release(state.Visual);
            }

            if (EntityManager.Exists(entity))
                EntityManager.DestroyEntity(entity);
        }

        private SkillContent BuildHitContext(SkillContent baseContext, Entity hitEntity, float3 hitPosition)
        {
            SkillContent context = baseContext.Clone();
            context.EntityManager = EntityManager;
            context.HasPosition = true;
            context.Position = new Vector3(hitPosition.x, hitPosition.y, hitPosition.z);
            context.HasTargetEntity = true;
            context.TargetEntity = hitEntity;
            context.HasTarget = false;
            context.Target = null;
            return context;
        }

        private static float3 GetProjectilePosition(SkillProjectileRegistry.State state)
        {
            if (state.Visual != null)
            {
                Vector3 position = state.Visual.transform.position;
                return new float3(position.x, position.y, position.z);
            }

            if (state.Context.HasPosition)
            {
                Vector3 position = state.Context.Position;
                return new float3(position.x, position.y, position.z);
            }

            return float3.zero;
        }
    }
}
