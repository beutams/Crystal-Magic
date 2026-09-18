using System.Collections.Generic;
using CrystalMagic.Core;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
public partial class ClientEntityLifetimePresentationSystem : SystemBase
{
    private const double DeathPresentationSeconds = 1d;

    protected override void OnUpdate()
    {
        double realtime = UnityEngine.Time.realtimeSinceStartupAsDouble;
        List<Entity> pendingDestroy = null;
        foreach ((RefRW<ClientEntityLifetimePresentationComponent> lifetimeRef, Entity entity) in
                 SystemAPI.Query<RefRW<ClientEntityLifetimePresentationComponent>>().WithEntityAccess())
        {
            ClientEntityLifetimePresentationComponent lifetime = lifetimeRef.ValueRO;
            bool isDead = EntityManager.HasComponent<UnitDeathComponent>(entity) &&
                          EntityManager.IsComponentEnabled<UnitDeathComponent>(entity);
            if (isDead && lifetime.DeathStarted == 0)
            {
                lifetime.DeathStarted = 1;
                lifetime.DestroyAtRealtime = realtime + DeathPresentationSeconds;
                BeginDeathAnimation(entity);
                EventComponent.Instance?.Publish(new UnitDiedEvent(entity));
            }

            if (isDead && lifetime.DeathStarted != 0 && lifetime.DeathDurationResolved == 0)
                ResolveDeathDuration(entity, realtime, ref lifetime);

            if (lifetime.DespawnRequested != 0 && !isDead)
                lifetime.DestroyAtRealtime = realtime;

            if (lifetime.DestroyAtRealtime > 0d && realtime >= lifetime.DestroyAtRealtime)
            {
                pendingDestroy ??= new List<Entity>();
                pendingDestroy.Add(entity);
            }

            lifetimeRef.ValueRW = lifetime;
        }

        MarkForDestroy(pendingDestroy);
    }

    private void BeginDeathAnimation(Entity entity)
    {
        if (!EntityManager.HasComponent<UnitAnimationComponent>(entity))
            return;

        UnitAnimationComponent animation = EntityManager.GetComponentData<UnitAnimationComponent>(entity);
        animation.AnimationName = new FixedString64Bytes("Death");
        animation.StartFrame = FrameManagerUtility.TryGet(EntityManager, out FrameManager frameManager)
            ? frameManager.currentFrame
            : 0u;
        animation.Sequence++;
        if (animation.Sequence == 0u)
            animation.Sequence = 1u;
        animation.RequestedStartElapsedSeconds = 0f;
        EntityManager.SetComponentData(entity, animation);
    }

    private void ResolveDeathDuration(
        Entity entity,
        double realtime,
        ref ClientEntityLifetimePresentationComponent lifetime)
    {
        if (!EntityManager.HasComponent<UnitAnimationComponent>(entity))
            return;

        UnitAnimationComponent animation = EntityManager.GetComponentData<UnitAnimationComponent>(entity);
        if (animation.CurrentClipLength <= 0f ||
            !animation.PlayingAnimationName.Equals(new FixedString64Bytes("Death")))
        {
            return;
        }

        float remainingSeconds = math.max(
            0f,
            animation.CurrentClipLength - animation.ElapsedSeconds);
        lifetime.DestroyAtRealtime = realtime + remainingSeconds;
        lifetime.DeathDurationResolved = 1;
    }

    private void MarkForDestroy(List<Entity> entities)
    {
        if (entities == null)
            return;

        for (int index = 0; index < entities.Count; index++)
        {
            Entity entity = entities[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
