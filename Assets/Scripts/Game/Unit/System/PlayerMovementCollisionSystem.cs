using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
partial struct PlayerMovementCollisionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UnitFactionComponent>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        if (deltaTime <= 0f)
            return;

        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        NativeList<ColliderCastHit> hits = new(Allocator.Temp);

        foreach ((RefRO<UnitFactionComponent> factionRef, RefRW<UnitMoveComponent> moveRef, RefRW<PhysicsVelocity> velocityRef,
            RefRO<PhysicsCollider> colliderRef, RefRO<LocalTransform> transformRef, Entity entity) in
            SystemAPI.Query<RefRO<UnitFactionComponent>, RefRW<UnitMoveComponent>, RefRW<PhysicsVelocity>, RefRO<PhysicsCollider>, RefRO<LocalTransform>>()
                .WithNone<UnitDeathComponent>()
                .WithEntityAccess())
        {
            if (!UnitFactionUtility.IsPlayer(factionRef.ValueRO.Value))
                continue;

            PhysicsCollider collider = colliderRef.ValueRO;
            PhysicsVelocity velocity = velocityRef.ValueRO;
            float3 displacement = velocity.Linear * deltaTime;
            displacement.z = 0f;

            if (!collider.Value.IsCreated || math.lengthsq(displacement) <= 0.00000001f)
                continue;

            LocalTransform transform = transformRef.ValueRO;
            ColliderCastInput input = new(
                collider.Value,
                transform.Position,
                transform.Position + displacement,
                transform.Rotation,
                math.max(0.0001f, math.abs(transform.Scale)));

            hits.Clear();
            if (!physicsWorld.CastCollider(input, ref hits))
                continue;

            if (!TryGetBlockingHit(hits, entity, out ColliderCastHit hit))
                continue;

            float3 normal = math.normalizesafe(hit.SurfaceNormal, float3.zero);
            float speedIntoSurface = math.dot(velocity.Linear, normal);
            if (speedIntoSurface >= 0f)
                continue;

            velocity.Linear -= normal * speedIntoSurface;
            velocity.Linear.z = 0f;
            velocity.Angular = float3.zero;
            velocityRef.ValueRW = velocity;

            UnitMoveComponent move = moveRef.ValueRO;
            move.Velocity = velocity.Linear.xy;
            moveRef.ValueRW = move;
        }

        hits.Dispose();
    }

    private static bool TryGetBlockingHit(NativeList<ColliderCastHit> hits, Entity self, out ColliderCastHit closestHit)
    {
        closestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            ColliderCastHit hit = hits[i];
            if (hit.Entity == self || hit.Material.CollisionResponse != CollisionResponsePolicy.Collide &&
                hit.Material.CollisionResponse != CollisionResponsePolicy.CollideRaiseCollisionEvents)
            {
                continue;
            }

            if (!found || hit.Fraction < closestHit.Fraction)
            {
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }
}
