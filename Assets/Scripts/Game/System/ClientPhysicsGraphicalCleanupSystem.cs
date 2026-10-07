using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(TransformSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(SmoothRigidBodiesGraphicalMotion))]
public partial struct ClientPhysicsGraphicalCleanupSystem : ISystem
{
    private EntityQuery _smoothedBodies;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _smoothedBodies = new EntityQueryBuilder(Allocator.Temp)
            .WithAny<PhysicsGraphicalSmoothing, PhysicsGraphicalInterpolationBuffer>()
            .Build(ref state);
        state.RequireForUpdate(_smoothedBodies);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Client prediction/interpolation owns the visible transform. Physics smoothing
        // reserves LocalToWorld through a write group and applies a second smoothing.
        // Strip it from live instances so normal transforms also update future spawns.
        state.EntityManager.RemoveComponent(_smoothedBodies, new ComponentTypeSet(
            ComponentType.ReadWrite<PhysicsGraphicalSmoothing>(),
            ComponentType.ReadWrite<PhysicsGraphicalInterpolationBuffer>()));
    }
}
