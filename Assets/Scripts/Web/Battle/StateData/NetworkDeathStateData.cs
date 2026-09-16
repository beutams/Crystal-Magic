using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkDeathStateData : NetworkStateData
{
    public byte isDead;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        if (!context.EntityManager.HasComponent<UnitDeathComponent>(entity))
            context.EntityManager.AddComponent<UnitDeathComponent>(entity);

        context.EntityManager.SetComponentData(entity, new UnitDeathComponent { NetworkDirty = 0 });
        context.EntityManager.SetComponentEnabled<UnitDeathComponent>(entity, isDead != 0);
    }
}
