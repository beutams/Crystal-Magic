using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public sealed class NetworkEntityDespawnStateData : NetworkStateData
{
    public byte waitForDeathPresentation;
    public bool hasPosition;
    public float positionX;
    public float positionY;
    public float positionZ;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.IsClient || !context.TryGetEntity(unitId, out Entity entity))
            return;

        if (context.EntityManager.HasComponent<ClientPredictedProjectileLinkComponent>(entity))
            ClientProjectilePredictionUtility.Release(context.EntityManager,
                context.EntityManager.GetComponentData<ClientPredictedProjectileLinkComponent>(entity).Prediction);

        if (hasPosition)
        {
            float3 position = new(positionX, positionY, positionZ);
            if (context.EntityManager.HasComponent<SkillProjectileComponent>(entity))
                context.ApplyProjectilePosition(entity, position, finalPosition: true);
            else
                context.ApplyPosition(entity, position);
        }

        ClientEntityLifetimePresentationComponent lifetime = context.EntityManager
            .HasComponent<ClientEntityLifetimePresentationComponent>(entity)
            ? context.EntityManager.GetComponentData<ClientEntityLifetimePresentationComponent>(entity)
            : default;
        lifetime.RequestedFrame = context.Frame;
        lifetime.DespawnRequested = 1;
        if (waitForDeathPresentation == 0)
            lifetime.DestroyAtRealtime = context.ApplyRealtime;
        context.SetOrAdd(entity, lifetime);
    }
}
