using System.Collections.Generic;
using CrystalMagic.Game.Skill;
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
            float2 targetDirection = math.normalizesafe(move.Direction, float2.zero);
            if (math.lengthsq(targetDirection) > 0.0001f)
                facing.Direction = targetDirection;

            float targetSpeed = UnitModifierResolver.GetMoveSpeed(EntityManager, entity) * move.StateMoveMultiplier;
            float maxSpeed = math.abs(targetSpeed);
            float maxAcceleration = math.max(0f, UnitModifierResolver.GetMaxAcceleration(EntityManager, entity));
            float2 targetVelocity = targetDirection * targetSpeed;
            if (move.StateMoveMultiplier <= 0f)
                move.Velocity = float2.zero;
            else
                UpdateMoveVelocity(ref move, targetVelocity, maxAcceleration, maxSpeed, deltaTime);

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
