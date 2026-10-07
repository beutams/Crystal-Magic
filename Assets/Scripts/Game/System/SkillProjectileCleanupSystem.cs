using Unity.Collections;
using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Server;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SkillProjectileSimulationSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
[UpdateAfter(typeof(ClientPredictedProjectileSystem))]
[UpdateBefore(typeof(PersistentEffectSystem))]
[UpdateBefore(typeof(EffectExecutionSystem))]
public partial class SkillProjectileCleanupSystem : SystemBase
{
    private readonly List<Entity> _missingDestroyFlags = new();
    private EntityQuery _projectiles;

    protected override void OnCreate()
    {
        _projectiles = GetEntityQuery(typeof(SkillProjectileFrameResultComponent), typeof(SkillProjectilePayloadComponent),
            typeof(SkillProjectileComponent), typeof(LocalTransform));
    }

    protected override void OnUpdate()
    {
        _missingDestroyFlags.Clear();
        using NativeArray<Entity> entities = _projectiles.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in entities)
        {
            SkillProjectileFrameResultComponent result = EntityManager.GetComponentData<SkillProjectileFrameResultComponent>(entity);
            if (result.HasHit == 0 && result.ShouldDestroy == 0)
                continue;

            SkillProjectilePayloadComponent payload = EntityManager.GetComponentData<SkillProjectilePayloadComponent>(entity);
            SkillProjectileComponent projectile = EntityManager.GetComponentData<SkillProjectileComponent>(entity);
            SkillContent baseContext = EffectUtility.GetContext(EntityManager, in payload.Context);
            SkillContent hitContext = null;
            try
            {
                if (result.HasHit != 0)
                {
                    projectile.HitSequence++;
                    hitContext = BuildHitContext(baseContext, result.HitEntity, result.HitPosition);
                    hitContext.EffectIdentity = baseContext.EffectIdentity.Impact(1, projectile.HitSequence,
                        SkillEffectIdentity.GetNetworkId(EntityManager, result.HitEntity));
                    EffectUtility.Enqueue(EntityManager, payload.OnCollisionEffectListId, hitContext);
                }

                if (result.ShouldDestroy != 0)
                {
                    projectile.Ended = 1;
                    if (result.HasTerrainHit != 0 || result.DestroyUsesHitContext != 0)
                    {
                        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
                        transform.Position = result.HasTerrainHit != 0 ? result.TerrainHitPosition : result.HitPosition;
                        EntityManager.SetComponentData(entity, transform);
                    }
                    if (result.TriggerDestroyEffects != 0)
                    {
                        if (result.DestroyUsesHitContext != 0 && hitContext != null)
                        {
                            hitContext.EffectIdentity = baseContext.EffectIdentity.Impact(2, projectile.HitSequence,
                                SkillEffectIdentity.GetNetworkId(EntityManager, result.HitEntity));
                            EffectUtility.Enqueue(EntityManager, payload.OnDestroyEffectListId, hitContext);
                        }
                        else
                        {
                            SkillContent destroyContext =
                                BuildDestroyContext(baseContext, EntityManager.GetComponentData<LocalTransform>(entity).Position,
                                    result.HasTerrainHit != 0);
                            try
                            {
                                destroyContext.EffectIdentity = baseContext.EffectIdentity.Impact(2, projectile.HitSequence, default);
                                EffectUtility.Enqueue(
                                    EntityManager,
                                    payload.OnDestroyEffectListId,
                                    destroyContext);
                            }
                            finally
                            {
                                SkillContentReferencePool.Return(destroyContext);
                            }
                        }
                    }

                    EndVisual(entity);
                    if (EntityManager.HasComponent<ClientPredictedProjectileComponent>(entity))
                    {
                        ClientPredictedProjectileComponent prediction = EntityManager.GetComponentData<ClientPredictedProjectileComponent>(entity);
                        prediction.HasPredictedEnd = 1;
                        prediction.PredictedEndFrame = FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager frame)
                            ? frame.currentFrame : 0;
                        EntityManager.SetComponentData(entity, prediction);
                    }
                    else
                    {
                        ReleasePayload(ref payload);
                        MarkForDestroy(entity);
                    }
                }
                if (!EntityManager.HasComponent<ClientPredictedProjectileComponent>(entity))
                    NetworkPresentationEventUtility.EnqueueProjectileImpact(EntityManager, entity,
                        baseContext.EffectIdentity.Impact(3, projectile.HitSequence,
                            SkillEffectIdentity.GetNetworkId(EntityManager, result.HitEntity)),
                        result.ShouldDestroy != 0 ? EntityManager.GetComponentData<LocalTransform>(entity).Position : result.HitPosition,
                        result.HasHit != 0, result.ShouldDestroy != 0);
                EntityManager.SetComponentData(entity, payload);
                projectile.NetworkDirty = 1;
                EntityManager.SetComponentData(entity, projectile);
                EntityManager.SetComponentData(entity, default(SkillProjectileFrameResultComponent));
            }
            finally
            {
                if (hitContext != null)
                    SkillContentReferencePool.Return(hitContext);
                EffectUtility.ReturnContext(baseContext);
            }
        }

        AddMissingDestroyFlags();
    }

    private void ReleasePayload(ref SkillProjectilePayloadComponent payload)
    {
        EffectUtility.ReleaseAfterExecution(EntityManager, payload.OnCollisionEffectListId);
        EffectUtility.ReleaseAfterExecution(EntityManager, payload.OnDestroyEffectListId);
        if (payload.OwnsManagedContext != 0)
        {
            EffectDataBridgeUtility.UnregisterManagedContext(
                EntityManager,
                payload.Context.ManagedContextId);
        }

        payload.OnCollisionEffectListId = default;
        payload.OnDestroyEffectListId = default;
        payload.CollisionConditionState = SkillProjectileConditionState.None;
        payload.OwnsManagedContext = 0;
    }

    private void EndVisual(Entity projectileEntity)
    {
        if (!EntityManager.HasComponent<SkillProjectileVisualLinkComponent>(projectileEntity))
            return;

        Entity visualEntity =
            EntityManager.GetComponentData<SkillProjectileVisualLinkComponent>(projectileEntity).VisualEntity;
        SpriteEffectAnimationSystem.RequestEnd(EntityManager, visualEntity);
    }

    private void MarkForDestroy(Entity entity)
    {
        if (EntityManager.HasComponent<DestroyEntityFlag>(entity))
        {
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            return;
        }

        _missingDestroyFlags.Add(entity);
    }

    private void AddMissingDestroyFlags()
    {
        for (int index = 0; index < _missingDestroyFlags.Count; index++)
        {
            Entity entity = _missingDestroyFlags[index];
            if (!EntityManager.Exists(entity))
                continue;

            EntityManager.AddComponent<DestroyEntityFlag>(entity);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }

        _missingDestroyFlags.Clear();
    }

    private static SkillContent BuildHitContext(
        SkillContent baseContext,
        Entity hitEntity,
        float3 hitPosition)
    {
        SkillContent context = SkillContentReferencePool.Get(baseContext);
        context.HasPosition = true;
        context.Position = new UnityEngine.Vector3(hitPosition.x, hitPosition.y, hitPosition.z);
        context.HasTargetEntity = true;
        context.TargetEntity = hitEntity;
        context.HasTarget = false;
        context.Target = null;
        return context;
    }

    private static SkillContent BuildDestroyContext(SkillContent baseContext, float3 position, bool hitTerrain)
    {
        SkillContent context = SkillContentReferencePool.Get(baseContext);
        context.HasPosition = true;
        context.Position = new UnityEngine.Vector3(position.x, position.y, position.z);
        if (hitTerrain)
        {
            context.HasTargetEntity = false;
            context.TargetEntity = Entity.Null;
            context.HasTarget = false;
            context.Target = null;
        }
        return context;
    }
}
