using CrystalMagic.Game.Map;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup))]
[UpdateAfter(typeof(UnitAnimationSystem))]
public partial class UnitSpriteRendererSortingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((RefRO<UnitAnimationComponent> _, RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<UnitAnimationComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (!EntityManager.HasComponent<SpriteRenderer>(entity))
                continue;

            SpriteRenderer spriteRenderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
            spriteRenderer.sortingOrder = TileOcclusionSortAnchor.OrderForY(transform.ValueRO.Position.y);
        }
    }
}
