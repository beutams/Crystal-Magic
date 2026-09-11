using System.Collections.Generic;
using CrystalMagic.Core;
using Unity.Entities;

[RunInGameWorld(GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SpriteEffectAnimationSystem))]
partial class VfxLifetimeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        List<Entity> pendingDestroy = null;

        foreach ((RefRW<VfxLifetimeComponent> lifetime, Entity entity) in
                 SystemAPI.Query<RefRW<VfxLifetimeComponent>>().WithEntityAccess())
        {
            lifetime.ValueRW.RemainingSeconds -= deltaTime;
            if (lifetime.ValueRO.RemainingSeconds > 0f)
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
