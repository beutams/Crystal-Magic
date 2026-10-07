using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class ClientProjectilePredictionUtility
{
    public static Entity Find(EntityManager manager, in SkillEffectIdentity identity)
    {
        if (identity.Valid == 0) return Entity.Null;
        using EntityQuery query = manager.CreateEntityQuery(typeof(ClientPredictedProjectileComponent));
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in entities)
            if (manager.GetComponentData<ClientPredictedProjectileComponent>(entity).Identity.SameProjectile(identity))
                return entity;
        return Entity.Null;
    }

    public static void Link(EntityManager manager, Entity anchor, Entity authority)
    {
        ClientPredictedProjectileComponent prediction = manager.GetComponentData<ClientPredictedProjectileComponent>(anchor);
        prediction.Authority = authority;
        manager.SetComponentData(anchor, prediction);
        if (authority != Entity.Null && manager.Exists(authority))
            SpriteEffectSpawnUtility.SetOrAddComponentData(manager, authority,
                new ClientPredictedProjectileLinkComponent { Prediction = anchor });
    }

    public static void ApplyImpact(EntityManager manager, in ClientPresentationEventElement impact)
    {
        Entity anchor = Find(manager, impact.Identity);
        CancelImpacts(manager, impact.Identity, impact.Identity.ImpactTargetId, impact.Identity.ImpactSequence,
            keepMatching: true, finalImpact: impact.FlagA != 0, rangeEnd: impact.FlagB == 0);
        if (anchor == Entity.Null) return;
        if (impact.FlagA != 0) Release(manager, anchor);
    }

    public static void ApplySnapshot(NetworkStateApplyContext context, Entity authority,
        in SkillEffectIdentity identity, in SkillProjectileComponent projectile, float3 position, uint hitSequence)
    {
        EntityManager manager = context.EntityManager;
        Entity anchor = Find(manager, identity);
        if (anchor == Entity.Null) return;
        Link(manager, anchor, authority);
        ClientPredictedProjectileComponent prediction = manager.GetComponentData<ClientPredictedProjectileComponent>(anchor);
        // A snapshot from before the predicted impact cannot disprove that impact.
        if (prediction.HasPredictedEnd == 0 || projectile.Ended != 0 || context.Frame < prediction.PredictedEndFrame) return;
        CancelImpacts(manager, identity, default, 0, keepMatching: false);
        prediction.HasPredictedEnd = 0;
        manager.SetComponentData(anchor, prediction);
        manager.SetComponentData(anchor, projectile);
        LocalTransform transform = manager.GetComponentData<LocalTransform>(anchor);
        float lead = FrameManagerUtility.TryGet(manager, out ClientFrameManager frame)
            ? (float)math.max(0d, (frame.currentFrame - (double)context.Frame - 1) * frame.frameInterval / 1000d) : 0f;
        transform.Position = position + projectile.Direction * projectile.Speed * lead;
        manager.SetComponentData(anchor, transform);
        manager.GetBuffer<SkillProjectileHitEntityElement>(anchor).Clear();
        RestoreVisual(manager, anchor, prediction, transform);
    }

    public static void Release(EntityManager manager, Entity anchor)
    {
        if (anchor == Entity.Null || !manager.Exists(anchor)) return;
        if (manager.HasComponent<SkillProjectileVisualLinkComponent>(anchor))
            SpriteEffectAnimationSystem.RequestEnd(manager,
                manager.GetComponentData<SkillProjectileVisualLinkComponent>(anchor).VisualEntity);
        if (manager.HasComponent<SkillProjectilePayloadComponent>(anchor))
        {
            SkillProjectilePayloadComponent payload = manager.GetComponentData<SkillProjectilePayloadComponent>(anchor);
            EffectDataBridgeUtility.Unregister(manager, payload.OnCollisionEffectListId);
            EffectDataBridgeUtility.Unregister(manager, payload.OnDestroyEffectListId);
            if (payload.OwnsManagedContext != 0)
                EffectDataBridgeUtility.UnregisterManagedContext(manager, payload.Context.ManagedContextId);
        }
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        DynamicBuffer<ClientPredictedSkillVisualEventElement> journal = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        for (int index = 0; index < journal.Length; index++)
        {
            ClientPredictedSkillVisualEventElement entry = journal[index];
            if (entry.AnchorEntity != anchor) continue;
            entry.AnchorEntity = Entity.Null;
            journal[index] = entry;
        }
        manager.DestroyEntity(anchor);
    }

    private static void RestoreVisual(EntityManager manager, Entity anchor,
        in ClientPredictedProjectileComponent prediction, in LocalTransform transform)
    {
        if (manager.HasComponent<SkillProjectileVisualLinkComponent>(anchor))
        {
            Entity previous = manager.GetComponentData<SkillProjectileVisualLinkComponent>(anchor).VisualEntity;
            if (previous != Entity.Null && manager.Exists(previous)) manager.DestroyEntity(previous);
        }
        if (prediction.VisualPrefab.IsEmpty || !SpriteEffectSpawnUtility.TrySpawn(manager,
                prediction.VisualPrefab.ToString(), transform.Position, transform.Rotation,
                prediction.VisualScale, 0f, out Entity visual)) return;
        SpriteEffectSpawnUtility.SetOrAddComponentData(manager, visual, new EffectVisualFollowComponent
        {
            Target = anchor, Offset = prediction.VisualOffset, AlignRotation = 1, EndWhenTargetMissing = 1,
        });
        SpriteEffectSpawnUtility.SetOrAddComponentData(manager, anchor, new SkillProjectileVisualLinkComponent { VisualEntity = visual });
        SpriteEffectSpawnUtility.SetOrAddComponentData(manager, visual, new ClientPredictedSkillVisualComponent
        {
            Identity = prediction.Identity, Frame = prediction.Identity.CastFrame,
            RequestOrdinal = prediction.Identity.CastOrdinal, EffectOrdinal = unchecked((int)prediction.Identity.Path), SegmentOrdinal = 1,
        });
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        DynamicBuffer<ClientPredictedSkillVisualEventElement> events = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
        for (int index = 0; index < events.Length; index++)
        {
            ClientPredictedSkillVisualEventElement entry = events[index];
            if (entry.IsProjectile == 0 || !entry.Identity.Equals(prediction.Identity)) continue;
            entry.VisualEntity = visual;
            events[index] = entry;
            break;
        }
    }

    public static void CancelImpacts(EntityManager manager, in SkillEffectIdentity projectile,
        System.Guid target, uint sequence, bool keepMatching, bool finalImpact = true, bool rangeEnd = false)
    {
        Entity runtime = ClientSkillVisualPredictionUtility.GetOrCreateRuntimeEntity(manager);
        using NativeArray<ClientPredictedSkillVisualEventElement> snapshot =
            manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime).ToNativeArray(Allocator.Temp);
        foreach (ClientPredictedSkillVisualEventElement entry in snapshot)
        {
            if (entry.Identity.Phase == 0 || !entry.Identity.SameProjectile(projectile) || entry.IsConfirmed != 0)
                continue;
            if (keepMatching && (entry.Identity.ImpactSequence < sequence ||
                !finalImpact && entry.Identity.ImpactSequence != sequence ||
                rangeEnd && entry.Identity.Phase == 1 && entry.Identity.ImpactSequence <= sequence ||
                entry.Identity.ImpactSequence == sequence && entry.Identity.ImpactTargetId == target))
                continue;
            // Audio and shake cannot be undone; retain their identity to prevent replay.
            if (entry.Type is ClientPresentationEventType.Sound or ClientPresentationEventType.CameraShake) continue;
            DestroyVisuals(manager, entry.Identity);
            if (entry.VisualEntity != Entity.Null && manager.Exists(entry.VisualEntity)) manager.DestroyEntity(entry.VisualEntity);
            if (entry.AnchorEntity != Entity.Null && manager.Exists(entry.AnchorEntity)) Release(manager, entry.AnchorEntity);
            DynamicBuffer<ClientPredictedSkillVisualEventElement> events = manager.GetBuffer<ClientPredictedSkillVisualEventElement>(runtime);
            for (int index = events.Length - 1; index >= 0; index--)
                if (events[index].Identity.Equals(entry.Identity)) events.RemoveAt(index);
        }
    }

    private static void DestroyVisuals(EntityManager manager, in SkillEffectIdentity identity)
    {
        using EntityQuery query = manager.CreateEntityQuery(typeof(ClientPredictedSkillVisualComponent));
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in entities)
            if (!manager.HasComponent<ClientPredictedProjectileComponent>(entity) &&
                manager.GetComponentData<ClientPredictedSkillVisualComponent>(entity).Identity.Equals(identity))
                manager.DestroyEntity(entity);
    }
}
