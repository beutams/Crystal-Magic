using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// Revenge waves march in shared legs, gathering before the next leg, like patrols.
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(BehaviorTreeSystem))]
public partial struct DungeonRevengeSquadSystem : ISystem
{
    private EntityQuery _members, _players, _map;
    private NativeHashMap<int, RevengeSquadLeg> _legs;
    private int _revision;

    public void OnCreate(ref SystemState state)
    {
        _members = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<DungeonRevengeSquadMember>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<UnitVariableElement>(), ComponentType.ReadOnly<UnitNavigationComponent>() },
            None = new[] { ComponentType.ReadOnly<UnitDeathComponent>(), ComponentType.ReadOnly<DestroyEntityFlag>(),
                ComponentType.ReadOnly<UnitInitializationPendingTag>() },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        });
        _players = state.GetEntityQuery(ComponentType.ReadOnly<PlayerCharacterComponent>(), ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<BattleSpectatorComponent>(), ComponentType.Exclude<UnitDeathComponent>());
        _map = state.GetEntityQuery(ComponentType.ReadOnly<DungeonNavigationMapComponent>(), ComponentType.ReadOnly<DungeonNavigationCollisionWord>());
        _legs = new NativeHashMap<int, RevengeSquadLeg>(16, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        _legs.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        // A reused battle World may start another floor with the same wave numbers.
        if (_members.IsEmptyIgnoreFilter)
        {
            state.Dependency.Complete();
            _legs.Clear();
            return;
        }
        NativeArray<Entity> members = _members.ToEntityArray(Allocator.TempJob);
        NativeArray<Entity> players = _players.ToEntityArray(Allocator.TempJob);
        RevengeSquadJob job = new()
        {
            Members = members, Players = players, Legs = _legs, Revision = ++_revision,
            Membership = SystemAPI.GetComponentLookup<DungeonRevengeSquadMember>(true),
            Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            Navigation = SystemAPI.GetComponentLookup<UnitNavigationComponent>(),
            Moves = SystemAPI.GetComponentLookup<UnitMoveComponent>(),
            Variables = SystemAPI.GetBufferLookup<UnitVariableElement>(),
            Perception = SystemAPI.GetBufferLookup<UnitPerceptionUnitElement>(true),
            Collisions = SystemAPI.GetBufferLookup<DungeonNavigationCollisionWord>(true),
            Time = SystemAPI.Time.ElapsedTime,
        };
        if (!_map.IsEmptyIgnoreFilter)
        {
            job.MapEntity = _map.GetSingletonEntity();
            job.Map = state.EntityManager.GetComponentData<DungeonNavigationMapComponent>(job.MapEntity);
        }
        state.Dependency = job.Schedule(state.Dependency);
        state.Dependency = members.Dispose(state.Dependency);
        state.Dependency = players.Dispose(state.Dependency);
    }
}

public struct RevengeSquadLeg
{
    public Entity Target;
    public float3 Destination;
    public int Version, Revision;
    public double StartedAt;
}

[BurstCompile]
public struct RevengeSquadJob : IJob
{
    [ReadOnly] public NativeArray<Entity> Members, Players;
    [ReadOnly] public ComponentLookup<DungeonRevengeSquadMember> Membership;
    [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
    public ComponentLookup<UnitNavigationComponent> Navigation;
    public ComponentLookup<UnitMoveComponent> Moves;
    public BufferLookup<UnitVariableElement> Variables;
    [ReadOnly] public BufferLookup<UnitPerceptionUnitElement> Perception;
    [ReadOnly] public BufferLookup<DungeonNavigationCollisionWord> Collisions;
    public NativeHashMap<int, RevengeSquadLeg> Legs;
    public Entity MapEntity;
    public DungeonNavigationMapComponent Map;
    public int Revision;
    public double Time;

    private struct Group
    {
        public float3 Center;
        public float RadiusSq, Speed;
        public int Count;
        public bool Fighting;
    }

    public void Execute()
    {
        NativeHashMap<int, Group> groups = new(math.max(1, Members.Length), Allocator.Temp);
        foreach (Entity member in Members)
        {
            DungeonRevengeSquadMember membership = Membership[member];
            groups.TryGetValue(membership.SquadId, out Group group);
            group.Center += Transforms[member].Position;
            float radius = math.max(0.1f, Navigation[member].ClearanceRadius);
            group.RadiusSq += radius * radius;
            group.Speed = group.Count == 0 ? membership.Speed : math.min(group.Speed, membership.Speed);
            group.Count++;
            group.Fighting |= Bool(Variables[member], "ai.attack.locked") || HasEnemy(member);
            groups[membership.SquadId] = group;
        }
        foreach (var pair in groups)
        {
            int id = pair.Key;
            Group group = pair.Value;
            float3 center = group.Center / group.Count;
            Legs.TryGetValue(id, out RevengeSquadLeg leg);
            Entity target = leg.Target;
            if (!Players.Contains(target))
            {
                target = Entity.Null;
                float distance = float.MaxValue;
                foreach (Entity player in Players)
                {
                    float next = math.distancesq(center.xy, Transforms[player].Position.xy);
                    if (next < distance) { target = player; distance = next; }
                }
            }
            bool active = target != Entity.Null && !group.Fighting;
            float gatheringRadius = 1f + 2f * math.sqrt(group.RadiusSq);
            bool allArrived = leg.Version > 0;
            foreach (Entity member in Members)
                if (Membership[member].SquadId == id && !Arrived(member, in leg, gatheringRadius))
                    allArrived = false;
            if (active && (leg.Version == 0 || leg.Target != target || allArrived || Time - leg.StartedAt > 12))
            {
                float3 goal = Transforms[target].Position;
                float2 delta = goal.xy - center.xy;
                float3 destination = new(center.xy + math.normalizesafe(delta) * math.min(math.length(delta), math.max(6f, gatheringRadius * 2f)), center.z);
                destination = WalkableDestination(destination, goal);
                leg = new RevengeSquadLeg { Target = target, Destination = destination,
                    Version = leg.Version + 1, StartedAt = Time };
            }
            leg.Revision = Revision;
            Legs[id] = leg;
            foreach (Entity member in Members)
            {
                if (Membership[member].SquadId != id) continue;
                DynamicBuffer<UnitVariableElement> variables = Variables[member];
                UnitVariableSource.SetValue(variables, "dungeon.revenge.hasDestination", UnitSourceValue.FromBool(active));
                UnitVariableSource.SetValue(variables, "dungeon.revenge.speed", UnitSourceValue.FromFloat(math.max(0f, group.Speed)));
                if (!active)
                {
                    UnitNavigationComponent stopped = Navigation[member];
                    UnitNavigationUtility.Stop(ref stopped);
                    Navigation[member] = stopped;
                    if (Moves.TryGetComponent(member, out UnitMoveComponent stoppedMove))
                    {
                        stoppedMove.CommandMoveSpeed = -1f;
                        Moves[member] = stoppedMove;
                    }
                    continue;
                }
                bool arrived = Arrived(member, in leg, gatheringRadius);
                float3 destination = leg.Destination;
                if (arrived)
                {
                    destination = Transforms[member].Position;
                    if (UnitVariableSource.TryGetValue(variables, "dungeon.revenge.arrivedVersion", out UnitSourceValue savedVersion) &&
                        savedVersion.TryGetInt(out int version) && version == leg.Version &&
                        UnitVariableSource.TryGetValue(variables, "dungeon.revenge.destination", out UnitSourceValue held) &&
                        held.TryGetFloat3(out float3 heldPosition))
                        destination = heldPosition;
                    UnitVariableSource.SetValue(variables, "dungeon.revenge.arrivedVersion", UnitSourceValue.FromInt(leg.Version));
                }
                UnitVariableSource.SetValue(variables, "dungeon.revenge.destination", UnitSourceValue.FromFloat3(destination));
                // This also updates sleeping members, whose trees deliberately do not run.
                UnitNavigationComponent navigation = Navigation[member];
                UnitNavigationUtility.SetDestination(ref navigation, destination, 0.1f);
                Navigation[member] = navigation;
                if (Moves.TryGetComponent(member, out UnitMoveComponent move))
                {
                    move.CommandMoveSpeed = math.max(0f, group.Speed);
                    Moves[member] = move;
                }
            }
        }
        using NativeArray<int> keys = Legs.GetKeyArray(Allocator.Temp);
        foreach (int id in keys)
            if (Legs[id].Revision != Revision) Legs.Remove(id);
        groups.Dispose();
    }

    private bool Arrived(Entity member, in RevengeSquadLeg leg, float radius)
    {
        DynamicBuffer<UnitVariableElement> variables = Variables[member];
        if (UnitVariableSource.TryGetValue(variables, "dungeon.revenge.arrivedVersion", out UnitSourceValue value) &&
            value.TryGetInt(out int version) && version == leg.Version) return true;
        float2 position = Transforms[member].Position.xy;
        if (math.distancesq(position, leg.Destination.xy) > radius * radius) return false;
        if (MapEntity == Entity.Null) return true;
        NativeArray<DungeonNavigationCollisionWord> words = Collisions[MapEntity].AsNativeArray();
        int steps = math.max(1, (int)math.ceil(math.distance(position, leg.Destination.xy) / (Map.CellSize * 0.5f)));
        for (int step = 0; step <= steps; step++)
            if (DungeonNavigationMapUtility.IsBlocked(in Map, words,
                    DungeonNavigationMapUtility.WorldToCell(in Map, math.lerp(position, leg.Destination.xy, (float)step / steps))))
                return false;
        return true;
    }

    private float3 WalkableDestination(float3 destination, float3 fallback)
    {
        if (MapEntity == Entity.Null) return destination;
        NativeArray<DungeonNavigationCollisionWord> words = Collisions[MapEntity].AsNativeArray();
        int2 cell = DungeonNavigationMapUtility.WorldToCell(in Map, destination.xy);
        for (int radius = 0; radius <= 3; radius++)
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    int2 candidate = cell + new int2(x, y);
                    if (DungeonNavigationMapUtility.CanOccupy(in Map, words, candidate, 0.5f))
                        return new float3(DungeonNavigationMapUtility.CellToWorld(in Map, candidate), destination.z);
                }
        return fallback;
    }

    private bool HasEnemy(Entity member)
    {
        if (!Perception.TryGetBuffer(member, out DynamicBuffer<UnitPerceptionUnitElement> units)) return false;
        foreach (UnitPerceptionUnitElement unit in units)
            if (unit.Faction == UnitFactionType.Player && Players.Contains(unit.Value)) return true;
        return false;
    }

    private static bool Bool(in DynamicBuffer<UnitVariableElement> variables, in FixedString128Bytes key) =>
        UnitVariableSource.TryGetValue(variables, key, out UnitSourceValue value) && value.TryGetBool(out bool flag) && flag;
}
