using System.Collections.Generic;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
public partial class ClientVfxLifetimeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        List<Entity> pendingDestroy = null;
        foreach ((RefRW<VfxLifetimeComponent> lifetimeRef, Entity entity) in
                 SystemAPI.Query<RefRW<VfxLifetimeComponent>>().WithEntityAccess())
        {
            lifetimeRef.ValueRW.RemainingSeconds -= deltaTime;
            if (lifetimeRef.ValueRO.RemainingSeconds > 0f)
                continue;

            pendingDestroy ??= new List<Entity>();
            pendingDestroy.Add(entity);
        }

        if (pendingDestroy == null)
            return;

        for (int index = 0; index < pendingDestroy.Count; index++)
        {
            Entity entity = pendingDestroy[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
