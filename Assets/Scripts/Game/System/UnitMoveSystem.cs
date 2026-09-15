using System.Collections.Generic;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[RunInGameWorld(GameWorldKind.Town | GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillReleaseSystem))]
partial class UnitMoveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasPhysicsWorld = SystemAPI.HasSingleton<PhysicsWorldSingleton>();
        PhysicsWorldSingleton physicsWorld = hasPhysicsWorld
            ? SystemAPI.GetSingleton<PhysicsWorldSingleton>()
            : default;
        List<VfxArrival> pendingArrivals = null;
        List<Entity> pendingDestroy = null;
        foreach ((RefRW<UnitMoveComponent> moveRef,
                  RefRW<UnitFacingComponent> facingRef,
                  RefRW<PhysicsVelocity> physicsVelocityRef,
                  RefRW<LocalTransform> transformRef,
                  Entity entity) in
                 SystemAPI.Query<RefRW<UnitMoveComponent>, RefRW<UnitFacingComponent>, RefRW<PhysicsVelocity>, RefRW<LocalTransform>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            if (EntityManager.HasComponent<VfxArrivalComponent>(entity))
            {
                VfxArrivalComponent arrival = EntityManager.GetComponentObject<VfxArrivalComponent>(entity);
                arrival.Elapsed += deltaTime;
                float progress = arrival.Duration <= 0f
                    ? 1f
                    : math.saturate(arrival.Elapsed / arrival.Duration);

                LocalTransform vfxTransform = transformRef.ValueRO;
                vfxTransform.Position = math.lerp(arrival.StartPosition, arrival.EndPosition, progress);
                transformRef.ValueRW = vfxTransform;

                if (progress >= 1f)
                {
                    pendingArrivals ??= new List<VfxArrival>();
                    pendingArrivals.Add(new VfxArrival(
                        arrival.ArrivalContext,
                        arrival.EndPosition,
                        arrival.OnArrivalEffects));
                    pendingDestroy ??= new List<Entity>();
                    pendingDestroy.Add(entity);
                }

                continue;
            }

            UnitMoveComponent move = moveRef.ValueRO;
            UnitFacingComponent facing = facingRef.ValueRO;
            bool hasFrameVelocity = move.HasFrameVelocity != 0;
            float2 frameVelocity = move.FrameVelocity;
            move.HasFrameVelocity = 0;

            if (hasFrameVelocity)
            {
                // StateScript can supply an explicit velocity for this frame, such as knockback.
                // It is consumed immediately, so ordinary movement resumes if no graph writes it next frame.
                move.Velocity = frameVelocity;
            }
            else
            {
                float2 targetDirection = math.normalizesafe(move.Direction, float2.zero);
                if (math.lengthsq(targetDirection) > 0.0001f)
                    facing.Direction = targetDirection;

                float resolvedSpeed = move.CommandMoveSpeed >= 0f
                    ? move.CommandMoveSpeed
                    : UnitModifierResolver.GetMoveSpeed(EntityManager, entity);
                float targetSpeed = resolvedSpeed * move.StateMoveMultiplier;
                float maxSpeed = math.abs(targetSpeed);
                float maxAcceleration = math.max(0f, UnitModifierResolver.GetMaxAcceleration(EntityManager, entity));
                float2 targetVelocity = targetDirection * targetSpeed;
                if (move.StateMoveMultiplier <= 0f)
                    move.Velocity = float2.zero;
                else
                    UpdateMoveVelocity(ref move, targetVelocity, maxAcceleration, maxSpeed, deltaTime);
            }

            if (hasPhysicsWorld && EntityManager.HasComponent<PhysicsCollider>(entity))
            {
                PhysicsCollider collider = EntityManager.GetComponentData<PhysicsCollider>(entity);
                move.Velocity = ConstrainVelocityByCollision(
                    physicsWorld,
                    entity,
                    transformRef.ValueRO,
                    collider,
                    move.Velocity,
                    deltaTime);
            }

            PhysicsVelocity physicsVelocity = physicsVelocityRef.ValueRO;
            LocalTransform transform = transformRef.ValueRO;
            ApplyPlanarTransform(ref physicsVelocity, ref transform, move.Velocity);

            moveRef.ValueRW = move;
            facingRef.ValueRW = facing;
            physicsVelocityRef.ValueRW = physicsVelocity;
            transformRef.ValueRW = transform;
        }

        ExecuteVfxArrivals(pendingArrivals);
        MarkForDestroy(pendingDestroy);
    }

    private void ExecuteVfxArrivals(List<VfxArrival> arrivals)
    {
        if (arrivals == null)
            return;

        for (int index = 0; index < arrivals.Count; index++)
        {
            VfxArrival arrival = arrivals[index];
            SkillContent context = arrival.Context?.Clone();
            if (context == null)
                continue;

            context.EntityManager = EntityManager;
            context.HasPosition = true;
            context.Position = new Vector3(arrival.Position.x, arrival.Position.y, arrival.Position.z);
            SkillExecutor.ExecuteEffects(arrival.Effects, context);
        }
    }

    private void MarkForDestroy(List<Entity> entities)
    {
        if (entities == null)
            return;

        for (int index = 0; index < entities.Count; index++)
        {
            Entity entity = entities[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }

    private static void UpdateMoveVelocity(ref UnitMoveComponent move, float2 targetVelocity, float maxAcceleration, float maxSpeed, float deltaTime)
    {
        float2 difference = targetVelocity - move.Velocity;
        float differenceLength = math.length(difference);

        if (differenceLength > 0.0001f)
        {
            float step = maxAcceleration * deltaTime;
            if (step >= differenceLength)
                move.Velocity = targetVelocity;
            else
                move.Velocity += difference / differenceLength * step;
        }

        float velocityLength = math.length(move.Velocity);
        if (velocityLength > maxSpeed && velocityLength > 0.0001f)
            move.Velocity = move.Velocity / velocityLength * maxSpeed;
    }

    private static void ApplyPlanarTransform(ref PhysicsVelocity physicsVelocity, ref LocalTransform transform, float2 planarVelocity)
    {
        physicsVelocity.Linear = new float3(planarVelocity.x, planarVelocity.y, 0f);
        physicsVelocity.Angular = float3.zero;
        transform.Position.z = 0f;
    }

    private static float2 ConstrainVelocityByCollision(
        PhysicsWorldSingleton physicsWorld,
        Entity entity,
        LocalTransform transform,
        PhysicsCollider collider,
        float2 velocity,
        float deltaTime)
    {
        float3 displacement = new float3(velocity.x, velocity.y, 0f) * math.max(0f, deltaTime);
        if (math.lengthsq(displacement) <= 0.000001f || !collider.Value.IsCreated)
            return velocity;

        ColliderCastInput input = new ColliderCastInput(
            collider.Value,
            transform.Position,
            transform.Position + displacement,
            transform.Rotation,
            transform.Scale);
        NativeList<ColliderCastHit> hits = new NativeList<ColliderCastHit>(Allocator.Temp);
        bool hasHit = physicsWorld.CastCollider(input, ref hits);
        if (!hasHit)
        {
            hits.Dispose();
            return velocity;
        }

        float closestFraction = 1f;
        float2 closestNormal = float2.zero;
        for (int index = 0; index < hits.Length; index++)
        {
            ColliderCastHit hit = hits[index];
            if (hit.Entity == entity)
                continue;

            float2 normal = math.normalizesafe(hit.SurfaceNormal.xy, float2.zero);
            if (math.lengthsq(normal) <= 0.000001f || math.dot(velocity, normal) >= -0.0001f)
                continue;

            if (hit.Fraction < closestFraction)
            {
                closestFraction = hit.Fraction;
                closestNormal = normal;
            }
        }

        hits.Dispose();
        if (closestFraction >= 1f)
            return velocity;

        float2 slideVelocity = velocity - closestNormal * math.min(0f, math.dot(velocity, closestNormal));
        return slideVelocity * math.max(0f, closestFraction - 0.01f);
    }

    private sealed class VfxArrival
    {
        public VfxArrival(SkillContent context, float3 position, CrystalMagic.Game.Data.Effects.EffectData[] effects)
        {
            Context = context;
            Position = position;
            Effects = effects;
        }

        public SkillContent Context { get; }

        public float3 Position { get; }

        public CrystalMagic.Game.Data.Effects.EffectData[] Effects { get; }
    }
}
