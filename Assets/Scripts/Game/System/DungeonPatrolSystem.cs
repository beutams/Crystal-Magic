using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
public partial class DungeonInterestPointSpawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        List<Entity> pendingSpawns = null;
        foreach ((DungeonInterestPointComponent point, Entity entity) in
                 SystemAPI.Query<DungeonInterestPointComponent>().WithEntityAccess())
        {
            if (point != null && point.PatrolEnabled && point.SpawnPatrolRequested && !point.HasSpawnedPatrol)
            {
                pendingSpawns ??= new List<Entity>();
                pendingSpawns.Add(entity);
            }
        }

        if (pendingSpawns == null)
            return;

        for (int index = 0; index < pendingSpawns.Count; index++)
        {
            Entity entity = pendingSpawns[index];
            if (!EntityManager.Exists(entity) ||
                !EntityManager.HasComponent<DungeonInterestPointComponent>(entity))
            {
                continue;
            }

            DungeonInterestPointComponent point = EntityManager.GetComponentObject<DungeonInterestPointComponent>(entity);
            DungeonPatrolRuntimeUtility.TrySpawnPatrol(EntityManager, entity, point);
        }
    }
}

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(UnitNavigationSystem))]
public partial class DungeonPatrolMemberDecisionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((RefRW<UnitMoveComponent> moveRef,
                  RefRW<UnitNavigationComponent> navigationRef,
                  DungeonPatrolMemberComponent patrolMember,
                  Entity entity) in
                 SystemAPI.Query<RefRW<UnitMoveComponent>, RefRW<UnitNavigationComponent>, DungeonPatrolMemberComponent>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            if (IsPlayerPerceived(entity))
            {
                moveRef.ValueRW.CommandMoveSpeed = -1f;
                continue;
            }

            if (!DungeonPatrolRuntimeUtility.TryGetSharedPatrolData(
                    EntityManager,
                    entity,
                    out Entity target,
                    out float speed,
                    out float arrivalDistance) ||
                !EntityManager.Exists(target) ||
                !EntityManager.HasComponent<LocalTransform>(target) ||
                !EntityManager.HasComponent<LocalTransform>(entity))
            {
                UnitNavigationUtility.Stop(ref navigationRef.ValueRW);
                moveRef.ValueRW.CommandMoveSpeed = -1f;
                continue;
            }

            float3 targetPosition = EntityManager.GetComponentData<LocalTransform>(target).Position;
            UnitNavigationUtility.SetDestination(ref navigationRef.ValueRW, targetPosition, arrivalDistance);
            moveRef.ValueRW.CommandMoveSpeed = speed;
        }
    }

    private bool IsPlayerPerceived(Entity entity)
    {
        if (!EntityManager.HasBuffer<UnitPerceptionUnitElement>(entity))
            return false;

        DynamicBuffer<UnitPerceptionUnitElement> perceivedUnits = EntityManager.GetBuffer<UnitPerceptionUnitElement>(entity);
        for (int index = 0; index < perceivedUnits.Length; index++)
        {
            Entity candidate = perceivedUnits[index].Value;
            if (EntityManager.Exists(candidate) &&
                EntityManager.HasComponent<UnitFactionComponent>(candidate) &&
                EntityManager.GetComponentData<UnitFactionComponent>(candidate).Value == UnitFactionType.Player)
            {
                return true;
            }
        }

        return false;
    }
}
