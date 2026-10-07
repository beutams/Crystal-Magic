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
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct UnitBlockingFinalizeSystem : ISystem
{
    private EntityQuery _query;
    private NativeList<UnitBlockingBody> _bodies;

    public void OnCreate(ref SystemState state)
    {
        _query = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitMoveComponent, UnitFactionComponent, LocalTransform, PhysicsCollider, PhysicsVelocity>()
            .WithNone<UnitDeathComponent, BattleSpectatorComponent, UnitInitializationPendingTag, VfxArrivalComponent>()
            .Build(ref state);
        _bodies = new NativeList<UnitBlockingBody>(16, Allocator.Persistent);
        state.RequireForUpdate(_query);
        state.RequireForUpdate<UnitBlockingSnapshot>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        int count = _query.CalculateEntityCount();
        if (count > _bodies.Capacity)
            _bodies.Capacity = math.max(count, _bodies.Capacity * 2);
        state.Dependency = new UnitBlockingFinalizeJob
        {
            Bodies = _bodies,
            Snapshots = SystemAPI.GetSingletonBuffer<UnitBlockingSnapshot>(true),
            DeltaTime = SystemAPI.Time.DeltaTime,
            Transforms = SystemAPI.GetComponentLookup<LocalTransform>(),
            Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
            Moves = SystemAPI.GetComponentLookup<UnitMoveComponent>(),
        }.Schedule(state.Dependency);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (_bodies.IsCreated)
            _bodies.Dispose();
    }
}

[BurstCompile]
internal struct UnitBlockingFinalizeJob : IJob
{
    public NativeList<UnitBlockingBody> Bodies;
    [ReadOnly] public DynamicBuffer<UnitBlockingSnapshot> Snapshots;
    public float DeltaTime;
    public ComponentLookup<LocalTransform> Transforms;
    public ComponentLookup<PhysicsVelocity> Velocities;
    public ComponentLookup<UnitMoveComponent> Moves;

    public void Execute()
    {
        if (DeltaTime <= 0.00001f)
            return;
        Bodies.Clear();
        for (int i = 0; i < Snapshots.Length; i++)
        {
            UnitBlockingSnapshot start = Snapshots[i];
            Entity entity = start.Body.Entity;
            if (!Transforms.HasComponent(entity) || !Velocities.HasComponent(entity) || !Moves.HasComponent(entity))
                continue;
            Bodies.AddNoResize(new UnitBlockingBody
            {
                Body = start.Body, Faction = start.Faction, Writable = start.Writable != 0,
                Velocity = start.Writable != 0
                    ? (Transforms[entity].Position.xy - start.Body.WorldFromBody.pos.xy) / DeltaTime
                    : start.Velocity,
            });
        }
        // Enemy/enemy impulses can still push a front row into a player after
        // the pre-step solve. Only shorten the actual completed displacement;
        // never teleport a neighbor away or invent a sideways correction.
        UnitBlockingSolveJob.Solve(Bodies, DeltaTime, true);
        for (int i = 0; i < Bodies.Length; i++)
        {
            UnitBlockingBody body = Bodies[i];
            if (!body.Writable)
                continue;
            Entity entity = body.Body.Entity;
            LocalTransform transform = Transforms[entity];
            float2 position = body.Body.WorldFromBody.pos.xy + body.Velocity * DeltaTime;
            if (math.distancesq(position, transform.Position.xy) < 0.0000000001f)
                continue;
            transform.Position.xy = position;
            Transforms[entity] = transform;
            PhysicsVelocity velocity = Velocities[entity];
            velocity.Linear.xy = body.Velocity;
            Velocities[entity] = velocity;
            UnitMoveComponent move = Moves[entity];
            move.Velocity = body.Velocity;
            move.NetworkDirty = 1;
            Moves[entity] = move;
        }
    }
}
