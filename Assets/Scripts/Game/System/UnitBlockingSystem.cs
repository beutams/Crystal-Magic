using System;
using CrystalMagic.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct UnitBlockingSystem : ISystem
{
    private EntityQuery _query;
    private NativeList<UnitBlockingBody> _bodies;
    private Entity _snapshotEntity;

    public void OnCreate(ref SystemState state)
    {
        _query = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitMoveComponent, UnitFactionComponent, LocalTransform, PhysicsCollider, PhysicsVelocity>()
            .WithNone<UnitDeathComponent, BattleSpectatorComponent, UnitInitializationPendingTag, VfxArrivalComponent>()
            .Build(ref state);
        _bodies = new NativeList<UnitBlockingBody>(16, Allocator.Persistent);
        _snapshotEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddBuffer<UnitBlockingSnapshot>(_snapshotEntity);
        state.RequireForUpdate(_query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        _bodies.Clear();
        int count = _query.CalculateEntityCount();
        if (count > _bodies.Capacity)
            _bodies.Capacity = math.max(count, _bodies.Capacity * 2);
        DynamicBuffer<UnitBlockingSnapshot> snapshots =
            state.EntityManager.GetBuffer<UnitBlockingSnapshot>(_snapshotEntity);
        snapshots.EnsureCapacity(count);
        bool client = SystemAPI.TryGetSingleton(out GameWorldContextComponent context) &&
                      context.Role == GameWorldRole.Client;
        JobHandle gather = new UnitBlockingGatherJob
        {
            Bodies = _bodies.AsParallelWriter(),
            Client = client,
            LocalPlayers = SystemAPI.GetComponentLookup<NetworkPlayerComponent>(true),
            Statuses = SystemAPI.GetComponentLookup<BattlePlayerStatusComponent>(true),
        }.ScheduleParallel(_query, state.Dependency);
        state.Dependency = new UnitBlockingSolveJob
        {
            Bodies = _bodies,
            Snapshots = snapshots,
            DeltaTime = SystemAPI.Time.DeltaTime,
            Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
            Moves = SystemAPI.GetComponentLookup<UnitMoveComponent>(),
        }.Schedule(gather);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (_bodies.IsCreated)
            _bodies.Dispose();
        if (state.EntityManager.Exists(_snapshotEntity))
            state.EntityManager.DestroyEntity(_snapshotEntity);
    }
}

internal struct UnitBlockingBody : IComparable<UnitBlockingBody>
{
    public RigidBody Body;
    public UnitFactionType Faction;
    public float2 Velocity;
    public bool Writable;
    public int CompareTo(UnitBlockingBody other)
    {
        int index = Body.Entity.Index.CompareTo(other.Body.Entity.Index);
        return index != 0 ? index : Body.Entity.Version.CompareTo(other.Body.Entity.Version);
    }
}

[BurstCompile]
internal partial struct UnitBlockingGatherJob : IJobEntity
{
    public NativeList<UnitBlockingBody>.ParallelWriter Bodies;
    public bool Client;
    [ReadOnly] public ComponentLookup<NetworkPlayerComponent> LocalPlayers;
    [ReadOnly] public ComponentLookup<BattlePlayerStatusComponent> Statuses;

    private void Execute(Entity entity, in UnitMoveComponent move, in UnitFactionComponent faction,
        in LocalTransform transform, in PhysicsCollider collider, in PhysicsVelocity velocity)
    {
        if (!collider.Value.IsCreated || faction.Value == UnitFactionType.Friend ||
            faction.Value == UnitFactionType.Interactable ||
            (Statuses.TryGetComponent(entity, out BattlePlayerStatusComponent status) &&
             (status.IsSpectator || status.IsWaitingForTransition)))
            return;
        bool writable = !Client || LocalPlayers.HasComponent(entity);
        Bodies.AddNoResize(new UnitBlockingBody
        {
            Body = new RigidBody
            {
                Entity = entity, Collider = collider.Value, Scale = transform.Scale,
                WorldFromBody = new RigidTransform(transform.Rotation, transform.Position),
            },
            Faction = faction.Value,
            // Client proxies have zero physics velocity. Their network move state
            // still supplies the authoritative velocity for relative sweeps.
            Velocity = writable ? velocity.Linear.xy : move.Velocity,
            Writable = writable,
        });
    }
}

[BurstCompile]
internal struct UnitBlockingSolveJob : IJob
{
    public NativeList<UnitBlockingBody> Bodies;
    public DynamicBuffer<UnitBlockingSnapshot> Snapshots;
    public float DeltaTime;
    public ComponentLookup<PhysicsVelocity> Velocities;
    public ComponentLookup<UnitMoveComponent> Moves;

    public void Execute()
    {
        Bodies.Sort();
        Snapshots.Clear();
        for (int i = 0; i < Bodies.Length; i++)
        {
            UnitBlockingBody body = Bodies[i];
            Snapshots.Add(new UnitBlockingSnapshot
            {
                Body = body.Body, Faction = body.Faction, Velocity = body.Velocity,
                Writable = body.Writable ? (byte)1 : (byte)0,
            });
        }
        Solve(Bodies, DeltaTime, false);
        for (int i = 0; i < Bodies.Length; i++)
        {
            UnitBlockingBody body = Bodies[i];
            if (!body.Writable)
                continue;
            Entity entity = body.Body.Entity;
            PhysicsVelocity velocity = Velocities[entity];
            if (math.all(velocity.Linear.xy == body.Velocity))
                continue;
            velocity.Linear = new float3(body.Velocity, velocity.Linear.z);
            Velocities[entity] = velocity;
            UnitMoveComponent move = Moves[entity];
            move.Velocity = body.Velocity;
            move.NetworkDirty = 1;
            Moves[entity] = move;
        }
    }

    internal static void Solve(NativeList<UnitBlockingBody> bodies, float deltaTime, bool preserveDirection)
    {
        // Player/enemy pairs only: O(players * units), with swept AABB rejection
        // before exact collider queries. No all-monster pair scan or managed alloc.
        for (int pass = 0; pass < 8; pass++)
        {
            bool changed = false;
            for (int i = 0; i < bodies.Length; i++)
            {
                UnitBlockingBody player = bodies[i];
                if (player.Faction != UnitFactionType.Player)
                    continue;
                for (int j = 0; j < bodies.Length; j++)
                {
                    UnitBlockingBody enemy = bodies[j];
                    if (!UnitBlockingUtility.IsHostilePair(player.Faction, enemy.Faction) ||
                        (!player.Writable && !enemy.Writable))
                        continue;
                    if (UnitBlockingUtility.Constrain(in player.Body, in enemy.Body, deltaTime,
                            ref player.Velocity, ref enemy.Velocity, player.Writable, enemy.Writable, preserveDirection))
                    {
                        bodies[j] = enemy;
                        changed = true;
                    }
                }
                bodies[i] = player;
            }
            if (!changed)
                break;
        }
    }
}
