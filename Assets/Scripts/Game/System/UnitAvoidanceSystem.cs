using CrystalMagic.ThirdParty.RVO2;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
[UpdateBefore(typeof(UnitMoveSystem))]
partial struct UnitAvoidanceSystem : ISystem
{
    private EntityQuery _agentQuery;
    private NativeParallelHashMap<Entity, AgentData> _agents;

    public void OnCreate(ref SystemState state)
    {
        _agentQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<UnitAvoidanceComponent>(),
                ComponentType.ReadOnly<UnitNavigationComponent>(),
                ComponentType.ReadOnly<UnitMoveComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
            },
            None = new[]
            {
                ComponentType.ReadOnly<UnitDeathComponent>(),
            },
        });
        _agents = new NativeParallelHashMap<Entity, AgentData>(16, Allocator.Persistent);
        state.RequireForUpdate<UnitQuerySingleton>();
        state.RequireForUpdate<UnitAvoidanceComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        UnitQuerySingleton query = SystemAPI.GetSingleton<UnitQuerySingleton>();
        if (query.UnitGridEntity == Entity.Null ||
            !state.EntityManager.Exists(query.UnitGridEntity) ||
            !state.EntityManager.HasBuffer<UnitQueryEntry>(query.UnitGridEntity))
        {
            return;
        }

        state.Dependency.Complete();
        int agentCount = _agentQuery.CalculateEntityCount();
        if (agentCount > _agents.Capacity)
            _agents.Capacity = math.max(agentCount, _agents.Capacity * 2);
        _agents.Clear();

        JobHandle prepareHandle = new UnitAvoidancePrepareJob
        {
            Agents = _agents.AsParallelWriter(),
            Modifiers = SystemAPI.GetComponentLookup<UnitModifierComponent>(true),
            DeltaTime = math.max(0.00001f, SystemAPI.Time.DeltaTime),
        }.ScheduleParallel(_agentQuery, state.Dependency);

        DynamicBuffer<UnitQueryEntry> unitEntries =
            state.EntityManager.GetBuffer<UnitQueryEntry>(query.UnitGridEntity, true);
        state.Dependency = new UnitAvoidanceSolveJob
        {
            Agents = _agents,
            UnitEntries = unitEntries.AsNativeArray(),
            InverseCellSize = query.InverseCellSize,
            DeltaTime = math.max(0.00001f, SystemAPI.Time.DeltaTime),
        }.ScheduleParallel(prepareHandle);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (_agents.IsCreated)
            _agents.Dispose();
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitAvoidancePrepareJob : IJobEntity
{
    internal NativeParallelHashMap<Entity, AgentData>.ParallelWriter Agents;

    [ReadOnly]
    public ComponentLookup<UnitModifierComponent> Modifiers;

    public float DeltaTime;

    private void Execute(
        Entity entity,
        in UnitAvoidanceComponent avoidance,
        in UnitNavigationComponent navigation,
        in UnitMoveComponent move,
        in LocalTransform transform)
    {
        UnitModifierComponent modifiers = Modifiers.TryGetComponent(
            entity,
            out UnitModifierComponent resolvedModifiers)
            ? resolvedModifiers
            : UnitModifierComponent.CreateIdentity();
        float2 currentVelocity = ResolveCurrentVelocity(in move);
        float resolvedSpeed = move.CommandMoveSpeed >= 0f
            ? move.CommandMoveSpeed
            : UnitModifierResolver.GetMoveSpeed(in move, in modifiers);
        float maxSpeed = math.max(0f, math.abs(resolvedSpeed * move.StateMoveMultiplier));
        float2 targetDirection = math.normalizesafe(move.Direction, float2.zero);
        float2 targetVelocity = targetDirection * resolvedSpeed * move.StateMoveMultiplier;
        float maxAcceleration = math.max(
            0f,
            UnitModifierResolver.GetMaxAcceleration(in move, in modifiers));
        float2 preferredVelocity = move.HasFrameVelocity != 0
            ? currentVelocity
            : MoveTowards(currentVelocity, targetVelocity, maxAcceleration * DeltaTime, maxSpeed);

        if (move.HasFrameVelocity != 0)
            maxSpeed = math.max(maxSpeed, math.length(currentVelocity));

        Agents.TryAdd(entity, new AgentData
        {
            Entity = entity,
            Position = transform.Position.xy,
            Velocity = currentVelocity,
            PreferredVelocity = preferredVelocity,
            NeighborDistance = math.max(0f, avoidance.NeighborDistance),
            MaxNeighbors = math.max(0, avoidance.MaxNeighbors),
            TimeHorizon = math.max(0.00001f, avoidance.TimeHorizon),
            Radius = math.max(0.01f, navigation.ClearanceRadius + avoidance.RadiusPadding),
            MaxSpeed = maxSpeed,
            HasFrameVelocity = move.HasFrameVelocity,
        });
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

}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent))]
public partial struct UnitAvoidanceSolveJob : IJobEntity
{
    [ReadOnly]
    internal NativeParallelHashMap<Entity, AgentData> Agents;

    [ReadOnly]
    public NativeArray<UnitQueryEntry> UnitEntries;

    public float InverseCellSize;
    public float DeltaTime;

    private void Execute(Entity entity, ref UnitAvoidanceComponent avoidance)
    {
        if (!Agents.TryGetValue(entity, out AgentData self))
        {
            avoidance.ResolvedVelocity = float2.zero;
            avoidance.HasResolvedVelocity = 0;
            return;
        }

        FixedList4096Bytes<AgentNeighbor> neighbors = default;
        float rangeSq = self.NeighborDistance * self.NeighborDistance;
        if (self.MaxNeighbors > 0 && self.NeighborDistance > 0f)
        {
            int2 minCell = UnitQueryGrid.GetCell(
                self.Position - self.NeighborDistance,
                InverseCellSize);
            int2 maxCell = UnitQueryGrid.GetCell(
                self.Position + self.NeighborDistance,
                InverseCellSize);
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    AddCellNeighbors(
                        in self,
                        UnitQueryGrid.GetCellKey(new int2(x, y)),
                        ref neighbors,
                        ref rangeSq);
                }
            }
        }

        avoidance.ResolvedVelocity = OrcaSolver.ComputeNewVelocity(in self, in neighbors, DeltaTime);
        avoidance.HasResolvedVelocity = self.HasFrameVelocity == 0 ? (byte)1 : (byte)0;
    }

    private void AddCellNeighbors(
        in AgentData self,
        long cellKey,
        ref FixedList4096Bytes<AgentNeighbor> neighbors,
        ref float rangeSq)
    {
        if (!UnitQueryGrid.TryGetCellRange(
                UnitEntries,
                cellKey,
                out int startIndex,
                out int endIndex))
        {
            return;
        }

        for (int index = startIndex; index < endIndex; index++)
        {
            Entity otherEntity = UnitEntries[index].Entity;
            if (Agents.TryGetValue(otherEntity, out AgentData other))
                OrcaSolver.InsertNeighbor(in self, in other, ref neighbors, ref rangeSq);
        }
    }
}
