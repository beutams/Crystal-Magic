using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateBefore(typeof(UnitInitializationFinalizeSystem))]
public partial class DungeonSpawnInitializationSystem : SystemBase
{
    private EntityQuery _query;

    protected override void OnCreate()
    {
        _query = GetEntityQuery(ComponentType.ReadOnly<UnitSpawnInitializationComponent>());
        RequireForUpdate(_query);
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _query.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            UnitSpawnInitializationComponent initialization =
                EntityManager.GetComponentData<UnitSpawnInitializationComponent>(entity);

            if (EntityManager.HasComponent<UnitOwnerComponent>(entity))
            {
                Entity owner = EntityManager.GetComponentData<UnitOwnerComponent>(entity).Owner;
                bool isInterestPoint = owner != Entity.Null &&
                                       EntityManager.Exists(owner) &&
                                       EntityManager.HasComponent<DungeonInterestPointComponent>(owner);
                bool isMonster = EntityManager.HasComponent<DungeonMonsterSpawnComponent>(entity);
                if (isInterestPoint && isMonster)
                {
                    DungeonInterestPointUtility.AttachMember(
                        EntityManager,
                        entity,
                        owner,
                        true,
                        true);
                }
            }

            if (initialization.RestoreRuntimeState != 0)
                GameRuntimeStateUtility.TryRestoreDungeonUnit(EntityManager, entity);
        }

        EntityManager.RemoveComponent<UnitSpawnInitializationComponent>(_query);
    }
}
