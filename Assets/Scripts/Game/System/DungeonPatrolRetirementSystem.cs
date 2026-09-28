using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(UnitNavigationSystem))]
public partial class DungeonPatrolRetirementSystem : SystemBase
{
    private const float RetirementPlayerDistance = 18f;

    protected override void OnUpdate()
    {
        float retirementDistanceSq = RetirementPlayerDistance * RetirementPlayerDistance;
        EntityCommandBuffer commands = new(Allocator.Temp);

        foreach ((RefRO<UnitOwnerComponent> owner, RefRO<DungeonMonsterSpawnComponent> monster, RefRW<UnitMoveComponent> move, Entity entity) in
                 SystemAPI.Query<RefRO<UnitOwnerComponent>, RefRO<DungeonMonsterSpawnComponent>, RefRW<UnitMoveComponent>>()
                     .WithNone<UnitDeathComponent, DestroyEntityFlag>()
                     .WithEntityAccess())
        {
            if (monster.ValueRO.CountsAsPatrol == 0 ||
                owner.ValueRO.Owner == Entity.Null ||
                !EntityManager.Exists(owner.ValueRO.Owner) ||
                !EntityManager.HasComponent<DungeonInterestPointComponent>(owner.ValueRO.Owner))
            {
                continue;
            }

            if (!DungeonPatrolRuntimeUtility.IsEncounterDead(EntityManager, owner.ValueRO.Owner))
                continue;

            if (!EntityManager.HasComponent<LocalTransform>(entity) || HasPlayerNearby(entity, retirementDistanceSq))
            {
                move.ValueRW.CommandMoveSpeed = 0f;
                move.ValueRW.Direction = float2.zero;
                move.ValueRW.Velocity = float2.zero;
                if (EntityManager.HasComponent<UnitNavigationComponent>(entity))
                {
                    UnitNavigationComponent navigation = EntityManager.GetComponentData<UnitNavigationComponent>(entity);
                    UnitNavigationUtility.Stop(ref navigation);
                    EntityManager.SetComponentData(entity, navigation);
                }
                continue;
            }

            commands.AddComponent<DestroyEntityFlag>(entity);
            commands.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }

        commands.Playback(EntityManager);
        commands.Dispose();
    }

    private bool HasPlayerNearby(Entity unit, float distanceSq)
    {
        float3 position = EntityManager.GetComponentData<LocalTransform>(unit).Position;
        foreach ((RefRO<LocalTransform> playerTransform, RefRO<UnitFactionComponent> faction) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<UnitFactionComponent>>().WithNone<UnitDeathComponent>())
        {
            if (faction.ValueRO.Value != UnitFactionType.Player)
                continue;

            if (math.distancesq(position.xy, playerTransform.ValueRO.Position.xy) <= distanceSq)
                return true;
        }

        return false;
    }
}
