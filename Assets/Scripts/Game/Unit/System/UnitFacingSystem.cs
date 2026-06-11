using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitJumpArcSystem))]
partial struct UnitFacingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<UnitFacingComponent> facingRef, RefRO<UnitMoveComponent> moveRef) in
                 SystemAPI.Query<RefRW<UnitFacingComponent>, RefRO<UnitMoveComponent>>())
        {
            if (math.lengthsq(moveRef.ValueRO.Velocity) <= 0.0001f)
                continue;

            facingRef.ValueRW.Direction = math.normalize(moveRef.ValueRO.Velocity);
        }
    }
}
