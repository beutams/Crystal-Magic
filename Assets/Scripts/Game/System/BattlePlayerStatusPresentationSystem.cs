using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup))]
[UpdateBefore(typeof(UnitAnimationSystem))]
public partial class BattlePlayerStatusPresentationSystem : SystemBase
{
    private const float SpectatorAlpha = 0.45f;

    protected override void OnUpdate()
    {
        foreach ((RefRO<BattlePlayerStatusComponent> status, Entity entity) in
                 SystemAPI.Query<RefRO<BattlePlayerStatusComponent>>().WithEntityAccess())
        {
            if (status.ValueRO.IsSpectator && EntityManager.HasComponent<UnitAnimationComponent>(entity))
            {
                bool moving = EntityManager.HasComponent<UnitMoveComponent>(entity) &&
                              math.lengthsq(EntityManager.GetComponentData<UnitMoveComponent>(entity).Velocity) > 0.0001f;
                FixedString64Bytes name = moving ? new FixedString64Bytes("Move") : new FixedString64Bytes("Idle");
                if (moving && EntityManager.HasComponent<UnitFacingComponent>(entity))
                {
                    UnitFacingComponent facing = EntityManager.GetComponentData<UnitFacingComponent>(entity);
                    facing.Direction = math.normalizesafe(EntityManager.GetComponentData<UnitMoveComponent>(entity).Velocity);
                    EntityManager.SetComponentData(entity, facing);
                }
                UnitAnimationComponent animation = EntityManager.GetComponentData<UnitAnimationComponent>(entity);
                if (!animation.AnimationName.Equals(name))
                {
                    animation.AnimationName = name;
                    animation.Sequence++;
                    animation.RequestedStartElapsedSeconds = 0f;
                    animation.ElapsedSeconds = 0f;
                    EntityManager.SetComponentData(entity, animation);
                }
            }
            if (!EntityManager.HasComponent<SpriteRenderer>(entity))
                continue;

            SpriteRenderer renderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
            Color color = renderer.color;
            float targetAlpha = status.ValueRO.IsFaded ? SpectatorAlpha : 1f;
            if (Mathf.Approximately(color.a, targetAlpha))
                continue;

            color.a = targetAlpha;
            renderer.color = color;
        }
    }
}
