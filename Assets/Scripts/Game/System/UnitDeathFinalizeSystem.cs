using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
[UpdateBefore(typeof(UnitDropOnDestroySystem))]
partial class UnitDeathFinalizeSystem : SystemBase
{
    private Entity _interactionEntity;
    private EntityQuery _deathQuery;

    protected override void OnCreate()
    {
        _interactionEntity = GameSingletonUtility.GetEntity<GameInteractionComponent>(EntityManager);
        _deathQuery = GetEntityQuery(ComponentType.ReadOnly<UnitDeathComponent>());
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _deathQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (EntityManager.HasComponent<DestroyEntityFlag>(entity) &&
                EntityManager.IsComponentEnabled<DestroyEntityFlag>(entity))
            {
                continue;
            }

            if (EntityManager.HasComponent<UnitOwnerComponent>(entity))
            {
                Entity owner = EntityManager.GetComponentData<UnitOwnerComponent>(entity).Owner;
                if (owner != Entity.Null)
                {
                    UnitOwnerMemberDeathEvent ownerEvent = new() { Owner = owner };
                    if (EntityManager.HasComponent<UnitOwnerMemberDeathEvent>(entity))
                        EntityManager.SetComponentData(entity, ownerEvent);
                    else
                        EntityManager.AddComponentData(entity, ownerEvent);
                }
            }

            GameInteractionUtility.FailTarget(EntityManager, _interactionEntity, entity);
            EventComponent.Instance?.Publish(new UnitDiedEvent(entity));

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
