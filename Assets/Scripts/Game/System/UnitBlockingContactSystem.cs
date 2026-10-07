using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PhysicsSimulationGroup))]
[UpdateAfter(typeof(PhysicsCreateContactsGroup))]
[UpdateBefore(typeof(PhysicsCreateJacobiansGroup))]
public partial struct UnitBlockingContactSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<SimulationSingleton>();
        state.RequireForUpdate<UnitMoveComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorld world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        state.Dependency = new UnitBlockingContactJob
        {
            Factions = SystemAPI.GetComponentLookup<UnitFactionComponent>(true),
            Moves = SystemAPI.GetComponentLookup<UnitMoveComponent>(true),
            Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
            Deaths = SystemAPI.GetComponentLookup<UnitDeathComponent>(true),
            Spectators = SystemAPI.GetComponentLookup<BattleSpectatorComponent>(true),
            Pending = SystemAPI.GetComponentLookup<UnitInitializationPendingTag>(true),
            Arrivals = SystemAPI.GetComponentLookup<VfxArrivalComponent>(true),
            Statuses = SystemAPI.GetComponentLookup<BattlePlayerStatusComponent>(true),
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
    }
}

[BurstCompile]
public struct UnitBlockingContactJob : IContactsJob
{
    [ReadOnly] public ComponentLookup<UnitFactionComponent> Factions;
    [ReadOnly] public ComponentLookup<UnitMoveComponent> Moves;
    [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
    [ReadOnly] public ComponentLookup<UnitDeathComponent> Deaths;
    [ReadOnly] public ComponentLookup<BattleSpectatorComponent> Spectators;
    [ReadOnly] public ComponentLookup<UnitInitializationPendingTag> Pending;
    [ReadOnly] public ComponentLookup<VfxArrivalComponent> Arrivals;
    [ReadOnly] public ComponentLookup<BattlePlayerStatusComponent> Statuses;

    public void Execute(ref ModifiableContactHeader header, ref ModifiableContactPoint contact)
    {
        if (Eligible(header.EntityA) && Eligible(header.EntityB) &&
            UnitBlockingUtility.IsHostilePair(Factions[header.EntityA].Value, Factions[header.EntityB].Value))
            header.JacobianFlags |= JacobianFlags.Disabled;
    }

    private bool Eligible(Entity entity) =>
        Factions.HasComponent(entity) && Moves.HasComponent(entity) && Velocities.HasComponent(entity) &&
        (!Deaths.HasComponent(entity) || !Deaths.IsComponentEnabled(entity)) &&
        !Spectators.HasComponent(entity) && !Pending.HasComponent(entity) && !Arrivals.HasComponent(entity) &&
        (!Statuses.TryGetComponent(entity, out BattlePlayerStatusComponent status) ||
         (!status.IsSpectator && !status.IsWaitingForTransition));
}
