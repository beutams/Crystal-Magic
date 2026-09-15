using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

[BurstCompile]
[RunInGameWorld(GameWorldKind.Town | GameWorldKind.Dungeon)]
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
        foreach (RefRW<PhysicsMassOverride> overrideRef in
            SystemAPI.Query<RefRW<PhysicsMassOverride>>()
                .WithAll<UnitFactionComponent>()
                .WithNone<UnitDeathComponent>())
        {
            PhysicsMassOverride massOverride = overrideRef.ValueRO;
            massOverride.IsKinematic = 1;
            massOverride.SetVelocityToZero = 0;
            overrideRef.ValueRW = massOverride;
        }

        foreach ((RefRW<PhysicsMass> massRef, RefRW<PhysicsVelocity> velocityRef) in
            SystemAPI.Query<RefRW<PhysicsMass>, RefRW<PhysicsVelocity>>()
                .WithAll<UnitFactionComponent>()
                .WithNone<UnitDeathComponent>())
        {
            PhysicsMass mass = massRef.ValueRO;
            mass.InverseMass = 0f;
            mass.InverseInertia = float3.zero;
            massRef.ValueRW = mass;

            PhysicsVelocity velocity = velocityRef.ValueRO;
            velocity.Angular = float3.zero;
            velocityRef.ValueRW = velocity;
        }
    }
}
