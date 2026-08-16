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
        foreach ((UnitAnimationComponent animation, RefRO<LocalTransform> transform) in
                 SystemAPI.Query<UnitAnimationComponent, RefRO<LocalTransform>>())
        {
            SpriteRenderer spriteRenderer = animation.Renderer;
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.ValueRO.Position.y * SortingPrecision);
        }
    }
}
