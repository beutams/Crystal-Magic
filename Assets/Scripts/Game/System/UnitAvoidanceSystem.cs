using CrystalMagic.ThirdParty.RVO2;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
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
        if (query.TreeEntity == Entity.Null ||
            !state.EntityManager.Exists(query.TreeEntity) ||
            !state.EntityManager.HasBuffer<UnitQueryNode>(query.TreeEntity) ||
            !state.EntityManager.HasBuffer<UnitQueryEntry>(query.TreeEntity))
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

        DynamicBuffer<UnitQueryNode> treeNodes =
            state.EntityManager.GetBuffer<UnitQueryNode>(query.TreeEntity, true);
        DynamicBuffer<UnitQueryEntry> treeEntries =
            state.EntityManager.GetBuffer<UnitQueryEntry>(query.TreeEntity, true);
        state.Dependency = new UnitAvoidanceSolveJob
        {
            Agents = _agents,
            Tree = new UnitQueryTree(treeNodes.AsNativeArray(), treeEntries.AsNativeArray()),
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
    public UnitQueryTree Tree;

    public float DeltaTime;

    private void Execute(Entity entity, ref UnitAvoidanceComponent avoidance)
    {
        if (!Agents.TryGetValue(entity, out AgentData self))
        {
            avoidance.ResolvedVelocity = float2.zero;
            avoidance.HasResolvedVelocity = 0;
            return;
        }

        AvoidanceVisitor visitor = new()
        {
            Self = self,
            Agents = Agents,
            RangeSq = self.NeighborDistance * self.NeighborDistance,
        };
        if (self.MaxNeighbors > 0 && self.NeighborDistance > 0f)
        {
            UnitQueryShape shape = UnitQueryShape.Circle(
                new float3(self.Position, 0f),
                self.NeighborDistance);
            Tree.Query(in shape, UnitFactionMask.Combatants, ref visitor);
        }

        avoidance.ResolvedVelocity = OrcaSolver.ComputeNewVelocity(in self, in visitor.Neighbors, DeltaTime);
        avoidance.HasResolvedVelocity = self.HasFrameVelocity == 0 ? (byte)1 : (byte)0;
    }

    private struct AvoidanceVisitor : IUnitQueryVisitor
    {
        public AgentData Self;

        [ReadOnly]
        public NativeParallelHashMap<Entity, AgentData> Agents;

        public FixedList4096Bytes<AgentNeighbor> Neighbors;
        public float RangeSq;

        public bool Visit(in UnitQueryEntry entry)
        {
            if (Agents.TryGetValue(entry.Entity, out AgentData other))
                OrcaSolver.InsertNeighbor(in Self, in other, ref Neighbors, ref RangeSq);
            return true;
        }
    }
}
