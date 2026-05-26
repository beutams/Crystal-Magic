using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(UnitJumpArcSystem))]
[UpdateBefore(typeof(UnitQueryBuildSystem))]
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

        foreach ((RefRW<UnitFacingComponent> facingRef, RefRW<LocalTransform> transformRef) in
                 SystemAPI.Query<RefRW<UnitFacingComponent>, RefRW<LocalTransform>>())
        {
            float2 direction = math.normalizesafe(facingRef.ValueRO.Direction, new float2(1f, 0f));
            facingRef.ValueRW.Direction = direction;

            LocalTransform transform = transformRef.ValueRO;
            transform.Rotation = UnitFacingUtility.CreateRotation(direction);
            transformRef.ValueRW = transform;
        }
    }
}
