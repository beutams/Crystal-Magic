using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
[UpdateAfter(typeof(UnitDeathFinalizeSystem))]
[UpdateBefore(typeof(UnitDropOnDestroySystem))]
public partial class DungeonInterestPointMemberDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((RefRO<UnitOwnerMemberDeathEvent> ownerEvent, Entity entity) in
                 SystemAPI.Query<RefRO<UnitOwnerMemberDeathEvent>>()
                     .WithAll<DungeonMonsterSpawnComponent>()
                     .WithEntityAccess())
        {
            if (ownerEvent.ValueRO.Owner == Entity.Null ||
                !EntityManager.Exists(ownerEvent.ValueRO.Owner) ||
                !EntityManager.HasComponent<DungeonInterestPointComponent>(ownerEvent.ValueRO.Owner))
            {
                continue;
            }

            DungeonInterestPointUtility.RemoveDeadMember(EntityManager, entity);
        }
    }
}
