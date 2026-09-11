using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class MoveVfxEffect : Effect
    {
        public new MoveVfxEffectData Data { get; }

        public MoveVfxEffect(MoveVfxEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || context == null || string.IsNullOrWhiteSpace(Data.VfxPrefabName) ||
                !SpriteEffectSpawnUtility.TryGetReleasePosition(context, out float3 releasePosition))
            {
                return;
            }

            float3 startOffset = new(Data.StartOffset.x, Data.StartOffset.y, Data.StartOffset.z);
            float3 moveOffset = new(Data.MoveOffset.x, Data.MoveOffset.y, Data.MoveOffset.z);
            float3 startPosition = releasePosition + startOffset;
            float3 endPosition = startPosition + moveOffset;
            if (!SpriteEffectSpawnUtility.TrySpawn(
                    context.EntityManager,
                    Data.VfxPrefabName,
                    startPosition,
                    quaternion.identity,
                    Data.Scale,
                    0f,
                    Data.PreservePrefabRotation,
                    out Entity effectEntity))
            {
                return;
            }

            SkillContent arrivalContext = context.Clone();
            arrivalContext.EntityManager = context.EntityManager;
            arrivalContext.HasPosition = true;
            arrivalContext.Position = new Vector3(endPosition.x, endPosition.y, endPosition.z);

            float duration = math.max(0f, Data.Duration);
            float2 planarMove = moveOffset.xy;
            float moveDistance = math.length(planarMove);
            UnitMoveComponent move = new()
            {
                BaseMoveSpeed = duration > 0f ? moveDistance / duration : 0f,
                BaseMoveSpeedOffset = 0f,
                BaseMaxAcceleration = float.MaxValue,
                Direction = math.normalizesafe(planarMove, float2.zero),
                StateMoveMultiplier = 1f,
                Velocity = float2.zero,
            };
            if (context.EntityManager.HasComponent<UnitMoveComponent>(effectEntity))
                context.EntityManager.SetComponentData(effectEntity, move);
            else
                context.EntityManager.AddComponentData(effectEntity, move);

            if (!context.EntityManager.HasComponent<UnitFacingComponent>(effectEntity))
            {
                context.EntityManager.AddComponentData(effectEntity, new UnitFacingComponent
                {
                    Direction = move.Direction,
                });
            }

            if (!context.EntityManager.HasComponent<PhysicsVelocity>(effectEntity))
                context.EntityManager.AddComponentData(effectEntity, default(PhysicsVelocity));

            VfxArrivalComponent arrival = new()
            {
                StartPosition = startPosition,
                EndPosition = endPosition,
                Duration = duration,
                ArrivalContext = arrivalContext,
                OnArrivalEffects = Data.OnArrivalEffects,
            };
            if (context.EntityManager.HasComponent<VfxArrivalComponent>(effectEntity))
            {
                VfxArrivalComponent existing = context.EntityManager.GetComponentObject<VfxArrivalComponent>(effectEntity);
                existing.StartPosition = arrival.StartPosition;
                existing.EndPosition = arrival.EndPosition;
                existing.Duration = arrival.Duration;
                existing.Elapsed = arrival.Elapsed;
                existing.ArrivalContext = arrival.ArrivalContext;
                existing.OnArrivalEffects = arrival.OnArrivalEffects;
            }
            else
            {
                context.EntityManager.AddComponentObject(effectEntity, arrival);
            }
        }
    }
}
