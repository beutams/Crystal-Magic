using System.Collections.Generic;
using CrystalMagic.ThirdParty.RVO2;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
[UpdateBefore(typeof(UnitMoveSystem))]
partial class UnitAvoidanceSystem : SystemBase
{
    private readonly Dictionary<Entity, Agent> _agentsByEntity = new();
    private readonly HashSet<Entity> _activeEntities = new();
    private readonly List<Entity> _orderedEntities = new();
    private readonly List<Entity> _removedEntities = new();
    private readonly List<UnitQueryHit> _queryHits = new();
    private EntityQuery _queryRuntimeQuery;

    protected override void OnCreate()
    {
        _queryRuntimeQuery = GetEntityQuery(
            ComponentType.ReadOnly<UnitQuerySingleton>(),
            ComponentType.ReadOnly<UnitQueryRuntimeComponent>());
        RequireForUpdate(_queryRuntimeQuery);
        RequireForUpdate<UnitAvoidanceComponent>();
    }

    protected override void OnUpdate()
    {
        float deltaTime = math.max(0.00001f, SystemAPI.Time.DeltaTime);
        Entity queryEntity = _queryRuntimeQuery.GetSingletonEntity();
        UnitQueryRuntimeComponent runtime = EntityManager.GetComponentObject<UnitQueryRuntimeComponent>(queryEntity);
        if (runtime?.UnitGrid == null)
            return;

        _activeEntities.Clear();
        _orderedEntities.Clear();

        foreach ((RefRO<UnitAvoidanceComponent> avoidanceRef,
                  RefRO<UnitNavigationComponent> navigationRef,
                  RefRO<UnitMoveComponent> moveRef,
                  RefRO<LocalTransform> transformRef,
                  Entity entity) in
                 SystemAPI.Query<
                         RefRO<UnitAvoidanceComponent>,
                         RefRO<UnitNavigationComponent>,
                         RefRO<UnitMoveComponent>,
                         RefRO<LocalTransform>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            _activeEntities.Add(entity);
            _orderedEntities.Add(entity);

            if (!_agentsByEntity.TryGetValue(entity, out Agent agent))
            {
                agent = new Agent();
                _agentsByEntity.Add(entity, agent);
            }

            UnitAvoidanceComponent avoidance = avoidanceRef.ValueRO;
            UnitNavigationComponent navigation = navigationRef.ValueRO;
            UnitMoveComponent move = moveRef.ValueRO;
            float2 currentVelocity = ResolveCurrentVelocity(move);
            float resolvedSpeed = move.CommandMoveSpeed >= 0f
                ? move.CommandMoveSpeed
                : UnitModifierResolver.GetMoveSpeed(EntityManager, entity);
            float maxSpeed = math.max(0f, math.abs(resolvedSpeed * move.StateMoveMultiplier));
            float2 targetDirection = math.normalizesafe(move.Direction, float2.zero);
            float2 targetVelocity = targetDirection * resolvedSpeed * move.StateMoveMultiplier;
            float maxAcceleration = math.max(0f, UnitModifierResolver.GetMaxAcceleration(EntityManager, entity));
            float2 preferredVelocity = move.HasFrameVelocity != 0
                ? currentVelocity
                : MoveTowards(currentVelocity, targetVelocity, maxAcceleration * deltaTime, maxSpeed);

            if (move.HasFrameVelocity != 0)
                maxSpeed = math.max(maxSpeed, math.length(currentVelocity));

            float radius = math.max(0.01f, navigation.ClearanceRadius + avoidance.RadiusPadding);
            agent.Configure(
                StableAgentId(entity),
                ToRvo(transformRef.ValueRO.Position.xy),
                ToRvo(currentVelocity),
                ToRvo(preferredVelocity),
                avoidance.NeighborDistance,
                avoidance.MaxNeighbors,
                avoidance.TimeHorizon,
                radius,
                maxSpeed);
        }

        RemoveInactiveAgents();
        _orderedEntities.Sort(CompareEntities);

        for (int entityIndex = 0; entityIndex < _orderedEntities.Count; entityIndex++)
        {
            Agent agent = _agentsByEntity[_orderedEntities[entityIndex]];
            agent.BeginNeighborQuery();
            if (agent.MaxNeighbors <= 0 || agent.NeighborDistance <= 0f)
                continue;

            runtime.UnitGrid.QueryCircle(
                new float3(agent.Position.X, agent.Position.Y, 0f),
                agent.NeighborDistance,
                _queryHits,
                false);

            float rangeSq = agent.NeighborDistance * agent.NeighborDistance;
            for (int hitIndex = 0; hitIndex < _queryHits.Count; hitIndex++)
            {
                Entity neighborEntity = _queryHits[hitIndex].Entity;
                if (_agentsByEntity.TryGetValue(neighborEntity, out Agent neighbor))
                    agent.InsertAgentNeighbor(neighbor, ref rangeSq);
            }
        }

        for (int entityIndex = 0; entityIndex < _orderedEntities.Count; entityIndex++)
            _agentsByEntity[_orderedEntities[entityIndex]].ComputeNewVelocity(deltaTime);

        for (int entityIndex = 0; entityIndex < _orderedEntities.Count; entityIndex++)
        {
            Entity entity = _orderedEntities[entityIndex];
            UnitAvoidanceComponent avoidance = EntityManager.GetComponentData<UnitAvoidanceComponent>(entity);
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
            Agent agent = _agentsByEntity[entity];
            avoidance.ResolvedVelocity = new float2(agent.NewVelocity.X, agent.NewVelocity.Y);
            avoidance.HasResolvedVelocity = move.HasFrameVelocity == 0 ? (byte)1 : (byte)0;
            EntityManager.SetComponentData(entity, avoidance);
        }
    }

    protected override void OnDestroy()
    {
        _agentsByEntity.Clear();
        _activeEntities.Clear();
        _orderedEntities.Clear();
        _removedEntities.Clear();
        _queryHits.Clear();
    }

    private void RemoveInactiveAgents()
    {
        _removedEntities.Clear();
        foreach (KeyValuePair<Entity, Agent> pair in _agentsByEntity)
        {
            if (!_activeEntities.Contains(pair.Key))
                _removedEntities.Add(pair.Key);
        }

        for (int index = 0; index < _removedEntities.Count; index++)
            _agentsByEntity.Remove(_removedEntities[index]);
    }

    private static float2 ResolveCurrentVelocity(in UnitMoveComponent move)
    {
        if (move.HasFrameVelocity != 0)
            return move.FrameVelocity;
        if (move.StateMoveMultiplier <= 0f)
            return float2.zero;
        return move.Velocity;
    }

    private static float2 MoveTowards(float2 current, float2 target, float maximumDelta, float maxSpeed)
    {
        float2 difference = target - current;
        float differenceLength = math.length(difference);
        float2 result = differenceLength <= maximumDelta || differenceLength <= 0.0001f
            ? target
            : current + difference / differenceLength * maximumDelta;

        float resultLength = math.length(result);
        if (resultLength > maxSpeed && resultLength > 0.0001f)
            result = result / resultLength * maxSpeed;
        return result;
    }

    private static Vector2 ToRvo(float2 value)
    {
        return new Vector2(value.x, value.y);
    }

    private static int StableAgentId(Entity entity)
    {
        return unchecked(entity.Index * 397 ^ entity.Version);
    }

    private static int CompareEntities(Entity left, Entity right)
    {
        int indexComparison = left.Index.CompareTo(right.Index);
        return indexComparison != 0 ? indexComparison : left.Version.CompareTo(right.Version);
    }
}
