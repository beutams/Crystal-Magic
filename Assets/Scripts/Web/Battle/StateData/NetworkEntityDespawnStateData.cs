using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkEntityDespawnStateData : NetworkStateData
{
    public byte waitForDeathPresentation;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.IsClient || !context.TryGetEntity(unitId, out Entity entity))
            return;

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
