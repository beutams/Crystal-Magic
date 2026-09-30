using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Resolve squad arrival before behavior trees read each member's destination.
// Combat and movement execution remain owned by the existing trees / navigation / ORCA.
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(BehaviorTreeSystem))]
public partial struct DungeonPatrolArrivalSystem : ISystem
{
    private EntityQuery _mapQuery;

    public void OnCreate(ref SystemState state)
    {
        _mapQuery = state.GetEntityQuery(ComponentType.ReadOnly<DungeonNavigationMapComponent>(),
            ComponentType.ReadOnly<DungeonNavigationCollisionWord>());
        state.RequireForUpdate<DungeonInterestPointComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        DungeonPatrolArrivalJob job = new()
        {
            Variables = SystemAPI.GetBufferLookup<UnitVariableElement>(),
            Owners = SystemAPI.GetComponentLookup<UnitVariableComponent>(true),
            Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            Navigations = SystemAPI.GetComponentLookup<UnitNavigationComponent>(true),
            Avoidance = SystemAPI.GetComponentLookup<UnitAvoidanceComponent>(true),
            Perceptions = SystemAPI.GetBufferLookup<UnitPerceptionUnitElement>(true),
            Deaths = SystemAPI.GetComponentLookup<UnitDeathComponent>(true),
            DestroyFlags = SystemAPI.GetComponentLookup<DestroyEntityFlag>(true),
            PendingInitialization = SystemAPI.GetComponentLookup<UnitInitializationPendingTag>(true),
            NavigationCollisions = SystemAPI.GetBufferLookup<DungeonNavigationCollisionWord>(true),
        };
        if (!_mapQuery.IsEmptyIgnoreFilter)
        {
            Entity mapEntity = _mapQuery.GetSingletonEntity();
            job.Map = state.EntityManager.GetComponentData<DungeonNavigationMapComponent>(mapEntity);
            job.MapEntity = mapEntity;
        }
        // Members and their shared count are written together. Never run owners in parallel.
        state.Dependency = job.Schedule(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(UnitDeathComponent), typeof(DestroyEntityFlag))]
public partial struct DungeonPatrolArrivalJob : IJobEntity
{
    public BufferLookup<UnitVariableElement> Variables;
    [ReadOnly] public ComponentLookup<UnitVariableComponent> Owners;
    [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
    [ReadOnly] public ComponentLookup<UnitNavigationComponent> Navigations;
    [ReadOnly] public ComponentLookup<UnitAvoidanceComponent> Avoidance;
    [ReadOnly] public BufferLookup<UnitPerceptionUnitElement> Perceptions;
    [ReadOnly] public ComponentLookup<UnitDeathComponent> Deaths;
    [ReadOnly] public ComponentLookup<DestroyEntityFlag> DestroyFlags;
    [ReadOnly] public ComponentLookup<UnitInitializationPendingTag> PendingInitialization;
    [ReadOnly] public BufferLookup<DungeonNavigationCollisionWord> NavigationCollisions;
    public Entity MapEntity;
    public DungeonNavigationMapComponent Map;

    private struct Member
    {
        public Entity Entity;
        public float3 Position;
        public float3 Destination;
        public float Radius;
        public bool Eligible;
        public bool Arrived;
    }

    private void Execute(Entity entity, in DungeonInterestPointComponent point,
        in DynamicBuffer<UnitVariableConsumerElement> consumers)
    {
        if (!Variables.TryGetBuffer(entity, out DynamicBuffer<UnitVariableElement> ownerVariables))
            return;
        float version = Number(ownerVariables, DungeonPatrolRuntimeUtility.PatrolTargetVersionKey, 0f);
        bool hasTarget = Transforms.TryGetComponent(point.PatrolTarget, out LocalTransform target) &&
                         math.all(math.isfinite(target.Position)) && !IsDead(point.PatrolTarget);
        bool active = hasTarget && Bool(ownerVariables, DungeonPatrolRuntimeUtility.PatrolActiveKey) &&
                      !Bool(ownerVariables, DungeonPatrolRuntimeUtility.EncounterDeadKey);
        float arrivalDistance = math.max(0.05f, Number(ownerVariables,
            DungeonPatrolRuntimeUtility.PatrolArrivalDistanceKey, point.ArrivalDistance));

        using NativeList<Member> members = new(consumers.Length, Allocator.Temp);
        using NativeList<int> frontier = new(consumers.Length, Allocator.Temp);
        float totalRadiusSq = 0f;
        for (int i = 0; i < consumers.Length; i++)
        {
            Entity member = consumers[i].Value;
            if (!Owners.TryGetComponent(member, out UnitVariableComponent owner) || owner.Other != entity ||
                !Variables.TryGetBuffer(member, out DynamicBuffer<UnitVariableElement> variables) ||
                !Bool(variables, DungeonPatrolRuntimeUtility.PatrolMemberKey) || IsDead(member) ||
                PendingInitialization.HasComponent(member) ||
                !Transforms.TryGetComponent(member, out LocalTransform transform) ||
                !math.all(math.isfinite(transform.Position)) ||
                !Navigations.TryGetComponent(member, out UnitNavigationComponent navigation))
                continue;

            bool eligible = active && !HasEnemy(member) && !Bool(variables, "dungeon.patrol.engaged") &&
                            !Bool(variables, "ai.attack.locked");
            bool arrived = active && Number(variables, DungeonPatrolRuntimeUtility.PatrolReportedVersionKey, -1f) == version;
            float radius = math.max(0.01f, navigation.ClearanceRadius);
            if (Avoidance.TryGetComponent(member, out UnitAvoidanceComponent avoidance))
                radius += math.max(0f, avoidance.RadiusPadding);
            totalRadiusSq += radius * radius;
            float3 destination = target.Position;
            if (arrived && UnitVariableSource.TryGetValue(variables, DungeonPatrolRuntimeUtility.PatrolDestinationKey,
                    out UnitSourceValue saved) && saved.TryGetFloat3(out float3 heldPosition))
                destination = heldPosition;
            else if (eligible && math.distancesq(transform.Position.xy, target.Position.xy) <= arrivalDistance * arrivalDistance &&
                     HasClearContact(transform.Position.xy, target.Position.xy))
            {
                arrived = true;
                destination = transform.Position;
            }
            members.Add(new Member
            {
                Entity = member, Position = transform.Position, Destination = destination,
                Radius = radius, Eligible = eligible, Arrived = arrived,
            });
        }

        // Compact gathering area grows with footprint, not with the length of a queue.
        // This prevents transitive arrival from travelling arbitrarily far down a line.
        float gatheringRadius = arrivalDistance + 2f * math.sqrt(totalRadiusSq);
        float gatheringRadiusSq = gatheringRadius * gatheringRadius;
        for (int i = 0; i < members.Length; i++)
            if (members[i].Arrived && members[i].Eligible &&
                math.distancesq(members[i].Position.xy, target.Position.xy) <= gatheringRadiusSq)
                frontier.Add(i);

        // Breadth-first propagation is independent of spawn / entity iteration order.
        for (int head = 0; head < frontier.Length; head++)
        {
            Member source = members[frontier[head]];
            for (int i = 0; i < members.Length; i++)
            {
                Member candidate = members[i];
                if (!candidate.Eligible || candidate.Arrived ||
                    math.distancesq(candidate.Position.xy, target.Position.xy) > gatheringRadiusSq)
                    continue;
                float contactDistance = source.Radius + candidate.Radius + 0.15f;
                if (math.distancesq(source.Position.xy, candidate.Position.xy) > contactDistance * contactDistance ||
                    !HasClearContact(source.Position.xy, candidate.Position.xy))
                    continue;
                candidate.Arrived = true;
                candidate.Destination = candidate.Position;
                members.ElementAt(i) = candidate;
                frontier.Add(i);
            }
        }

        int reachedCount = 0;
        for (int i = 0; i < members.Length; i++)
        {
            Member member = members[i];
            DynamicBuffer<UnitVariableElement> variables = Variables[member.Entity];
            UnitVariableSource.SetValue(variables, DungeonPatrolRuntimeUtility.PatrolHasDestinationKey, UnitSourceValue.FromBool(active));
            if (active)
                UnitVariableSource.SetValue(variables, DungeonPatrolRuntimeUtility.PatrolDestinationKey, UnitSourceValue.FromFloat3(member.Destination));
            if (!member.Arrived)
                continue;
            UnitVariableSource.SetValue(variables, DungeonPatrolRuntimeUtility.PatrolReportedVersionKey, UnitSourceValue.FromFloat(version));
            reachedCount++;
        }
        // Recount survivors instead of incrementing a shared counter from multiple trees.
        UnitVariableSource.SetValue(ownerVariables, DungeonPatrolRuntimeUtility.PatrolReachedCountKey, UnitSourceValue.FromInt(reachedCount));
    }

    private bool IsDead(Entity entity) =>
        (Deaths.HasComponent(entity) && Deaths.IsComponentEnabled(entity)) ||
        (DestroyFlags.HasComponent(entity) && DestroyFlags.IsComponentEnabled(entity));

    private bool HasEnemy(Entity entity)
    {
        if (!Perceptions.TryGetBuffer(entity, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return false;
        for (int i = 0; i < units.Length; i++)
            if (units[i].Faction == UnitFactionType.Player && !IsDead(units[i].Value) && Transforms.HasComponent(units[i].Value))
                return true;
        return false;
    }

    private static bool Bool(in DynamicBuffer<UnitVariableElement> variables, in FixedString128Bytes key) =>
        UnitVariableSource.TryGetValue(variables, key, out UnitSourceValue value) && value.TryGetBool(out bool flag) && flag;

    private static float Number(in DynamicBuffer<UnitVariableElement> variables, in FixedString128Bytes key, float fallback) =>
        UnitVariableSource.TryGetValue(variables, key, out UnitSourceValue value) &&
        value.TryGetNumber(out float number) && math.isfinite(number) ? number : fallback;

    private bool HasClearContact(float2 start, float2 end)
    {
        if (MapEntity == Entity.Null)
            return true;
        if (Map.CellSize <= 0f || Map.Width <= 0 || Map.Height <= 0)
            return false;
        NativeArray<DungeonNavigationCollisionWord> collisionWords = NavigationCollisions[MapEntity].AsNativeArray();
        int2 cell = (int2)math.floor((start - Map.WorldOrigin) / Map.CellSize);
        int2 goal = (int2)math.floor((end - Map.WorldOrigin) / Map.CellSize);
        float2 delta = end - start;
        int2 step = (int2)math.sign(delta);
        float2 tDelta = new(delta.x == 0f ? float.PositiveInfinity : Map.CellSize / math.abs(delta.x),
            delta.y == 0f ? float.PositiveInfinity : Map.CellSize / math.abs(delta.y));
        float2 edge = Map.WorldOrigin + (new float2(cell) + math.select(float2.zero, new float2(1f), step > 0)) * Map.CellSize;
        float2 tMax = new(delta.x == 0f ? float.PositiveInfinity : (edge.x - start.x) / delta.x,
            delta.y == 0f ? float.PositiveInfinity : (edge.y - start.y) / delta.y);
        int remaining = Map.Width + Map.Height + 2;
        while (remaining-- > 0)
        {
            if (DungeonNavigationMapUtility.IsBlocked(in Map, collisionWords, cell))
                return false;
            if (math.all(cell == goal))
                return true;
            if (math.abs(tMax.x - tMax.y) < 0.00001f)
            {
                if (DungeonNavigationMapUtility.IsBlocked(in Map, collisionWords, cell + new int2(step.x, 0)) ||
                    DungeonNavigationMapUtility.IsBlocked(in Map, collisionWords, cell + new int2(0, step.y)))
                    return false;
                cell += step;
                tMax += tDelta;
            }
            else if (tMax.x < tMax.y) { cell.x += step.x; tMax.x += tDelta.x; }
            else { cell.y += step.y; tMax.y += tDelta.y; }
        }
        return false;
    }
}
