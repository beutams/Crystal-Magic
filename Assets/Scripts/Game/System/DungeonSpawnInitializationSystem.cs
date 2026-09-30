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
                DungeonDifficultyUtility.Inherit(EntityManager, owner, entity);
                bool isInterestPoint = owner != Entity.Null &&
                                       EntityManager.Exists(owner) &&
                                       EntityManager.HasComponent<DungeonInterestPointComponent>(owner);
                bool isMonster = EntityManager.HasComponent<DungeonMonsterSpawnComponent>(entity);
                bool isGuard = isMonster && EntityManager.GetComponentData<DungeonMonsterSpawnComponent>(entity).CountsAsGuard != 0;
                bool isPatrol = UnitVariableSource.TryGetValue(EntityManager, entity,
                    DungeonPatrolRuntimeUtility.PatrolMemberKey, out UnitSourceValue patrolValue) &&
                    patrolValue.TryGetBool(out bool patrol) && patrol;
                if (isInterestPoint && !isGuard && (isMonster || isPatrol))
                {
                    if (!isMonster)
                    {
                        DungeonInterestPointComponent point = EntityManager.GetComponentData<DungeonInterestPointComponent>(owner);
                        EntityManager.AddComponentData(entity, new DungeonMonsterSpawnComponent
                        {
                            SaveId = -1, RegionId = point.EncounterId, SquadId = point.SquadId,
                        });
                    }
                    DungeonInterestPointUtility.AttachMember(
                        EntityManager,
                        entity,
                        owner,
                        false,
                        true);
                }
            }

            DungeonDifficultyUtility.ApplyHealth(EntityManager, entity);
            if (initialization.RestoreRuntimeState != 0)
                GameRuntimeStateUtility.TryRestoreDungeonUnit(EntityManager, entity);
            DungeonPatrolRuntimeUtility.CaptureGuardHome(EntityManager, entity);
        }

        EntityManager.RemoveComponent<UnitSpawnInitializationComponent>(_query);
    }
}
