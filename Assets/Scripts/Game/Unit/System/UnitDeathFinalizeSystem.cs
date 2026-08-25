using CrystalMagic.Core;
using Unity.Entities;

[UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
[UpdateBefore(typeof(UnitDropOnDestroySystem))]
[UpdateBefore(typeof(DestroyEntitySystem))]
partial class UnitDeathFinalizeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((EnabledRefRO<UnitDeathComponent> _, Entity entity) in
                 SystemAPI.Query<EnabledRefRO<UnitDeathComponent>>()
                     .WithEntityAccess())
        {
            if (EntityManager.HasComponent<DestroyEntityFlag>(entity) &&
                EntityManager.IsComponentEnabled<DestroyEntityFlag>(entity))
            {
                continue;
            }

            EventComponent.Instance?.Publish(new UnitDiedEvent(entity));

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
