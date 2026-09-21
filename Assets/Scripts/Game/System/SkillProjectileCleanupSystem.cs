using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
[UpdateBefore(typeof(PersistentEffectSystem))]
[UpdateBefore(typeof(EffectExecutionSystem))]
public partial class SkillProjectileCleanupSystem : SystemBase
{
    private readonly List<Entity> _missingDestroyFlags = new();

    protected override void OnUpdate()
    {
        _missingDestroyFlags.Clear();
        foreach ((RefRW<SkillProjectileFrameResultComponent> resultReference,
                     RefRW<SkillProjectilePayloadComponent> payloadReference,
                     RefRO<LocalTransform> transformReference,
                     Entity entity) in
                 SystemAPI.Query<RefRW<SkillProjectileFrameResultComponent>,
                         RefRW<SkillProjectilePayloadComponent>,
                         RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            SkillProjectileFrameResultComponent result = resultReference.ValueRO;
            if (result.HasHit == 0 && result.ShouldDestroy == 0)
                continue;

            SkillProjectilePayloadComponent payload = payloadReference.ValueRO;
            SkillContent baseContext = EffectUtility.CreateContext(EntityManager, in payload.Context);
            SkillContent hitContext = null;
            if (result.HasHit != 0)
            {
                hitContext = BuildHitContext(baseContext, result.HitEntity, result.HitPosition);
                EffectUtility.Enqueue(EntityManager, payload.OnCollisionEffectListId, hitContext);
            }

            if (result.ShouldDestroy != 0)
            {
                if (result.TriggerDestroyEffects != 0)
                {
                    SkillContent destroyContext = result.DestroyUsesHitContext != 0 && hitContext != null
                        ? hitContext
                        : BuildDestroyContext(baseContext, transformReference.ValueRO.Position);
                    EffectUtility.Enqueue(EntityManager, payload.OnDestroyEffectListId, destroyContext);
                }

                ReleasePayload(ref payload);
                payloadReference.ValueRW = payload;
                EndVisual(entity);
                MarkForDestroy(entity);
            }

            resultReference.ValueRW = default;
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
        SkillContent context = baseContext.Clone();
        context.HasPosition = true;
        context.Position = new UnityEngine.Vector3(hitPosition.x, hitPosition.y, hitPosition.z);
        context.HasTargetEntity = true;
        context.TargetEntity = hitEntity;
        context.HasTarget = false;
        context.Target = null;
        return context;
    }

    private static SkillContent BuildDestroyContext(SkillContent baseContext, float3 position)
    {
        SkillContent context = baseContext.Clone();
        context.HasPosition = true;
        context.Position = new UnityEngine.Vector3(position.x, position.y, position.z);
        return context;
    }
}
