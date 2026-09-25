using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
partial struct UnitPhysicsRotationInitializationSystem : ISystem
{
    private EntityQuery _uninitializedUnitQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _uninitializedUnitQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnitFactionComponent>()
            .WithAllRW<PhysicsMass, PhysicsVelocity>()
            .WithNone<UnitDeathComponent, UnitInteractableComponent, UnitPhysicsRotationInitializedComponent>()
            .Build(ref state);

        state.RequireForUpdate(_uninitializedUnitQuery);
        state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter commandBuffer = SystemAPI
            .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new UnitPhysicsRotationInitializationJob
        {
            CommandBuffer = commandBuffer,
        }.ScheduleParallel(_uninitializedUnitQuery, state.Dependency);
    }
}

[BurstCompile]
public partial struct UnitPhysicsRotationInitializationJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter CommandBuffer;

    private void Execute(
        [EntityIndexInQuery] int sortKey,
        Entity entity,
        ref PhysicsMass mass,
        ref PhysicsVelocity velocity)
    {
        mass.InverseInertia = float3.zero;
        velocity.Angular = float3.zero;
        CommandBuffer.AddComponent<UnitPhysicsRotationInitializedComponent>(sortKey, entity);
    }
}
