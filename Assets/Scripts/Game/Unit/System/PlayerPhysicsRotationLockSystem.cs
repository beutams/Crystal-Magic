using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

[BurstCompile]
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
partial struct PlayerPhysicsRotationLockSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitFactionComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRO<UnitFactionComponent> factionRef, RefRW<PhysicsMass> massRef, RefRW<PhysicsVelocity> velocityRef) in
            SystemAPI.Query<RefRO<UnitFactionComponent>, RefRW<PhysicsMass>, RefRW<PhysicsVelocity>>()
                .WithNone<UnitDeathComponent>())
        {
            if (!UnitFactionUtility.IsPlayer(factionRef.ValueRO.Value))
                continue;

            PhysicsMass mass = massRef.ValueRO;
            mass.InverseInertia = float3.zero;
            massRef.ValueRW = mass;

            PhysicsVelocity velocity = velocityRef.ValueRO;
            velocity.Angular = float3.zero;
            velocityRef.ValueRW = velocity;
        }
    }
}
