using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitAnimationSystem))]
public partial class UnitSpriteRendererSortingSystem : SystemBase
{
    private const float SortingPrecision = 100f;

    protected override void OnUpdate()
    {
        foreach ((RefRO<LocalTransform> transform, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>().WithAll<UnitAnimationComponent>().WithEntityAccess())
        {
            if (!EntityManager.HasComponent<SpriteRenderer>(entity))
                continue;

            SpriteRenderer spriteRenderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.ValueRO.Position.y * SortingPrecision);
        }
    }
}
