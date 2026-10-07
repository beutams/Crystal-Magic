using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct ClientPhysicsProxySystem : ISystem
{
    private EntityQuery _newProxies;
    private EntityQuery _ownedPlayers;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _newProxies = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PhysicsVelocity>().WithNone<NetworkPlayerComponent, PhysicsMassOverride>()
            .Build(ref state);
        _ownedPlayers = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<NetworkPlayerComponent, PhysicsMassOverride>().Build(ref state);
        state.RequireForUpdate<PhysicsVelocity>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Remote entities provide colliders at their latest authoritative positions.
        // They must not drift or get pushed by each local replay step.
        state.EntityManager.AddComponent<PhysicsMassOverride>(_newProxies);
        state.EntityManager.RemoveComponent<PhysicsMassOverride>(_ownedPlayers);
        state.Dependency = new ClientPhysicsProxyJob
        {
            Interpolations = SystemAPI.GetComponentLookup<ClientTransformInterpolationComponent>(true),
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithNone(typeof(NetworkPlayerComponent))]
public partial struct ClientPhysicsProxyJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<ClientTransformInterpolationComponent> Interpolations;

    private void Execute(Entity entity, ref PhysicsMassOverride massOverride,
        ref PhysicsVelocity velocity, ref LocalTransform transform)
    {
        massOverride.IsKinematic = 1;
        massOverride.SetVelocityToZero = 1;
        velocity = default;
        if (Interpolations.TryGetComponent(entity, out ClientTransformInterpolationComponent interpolation) &&
            interpolation.Initialized != 0)
            transform.Position = interpolation.TargetPosition;
    }
}
