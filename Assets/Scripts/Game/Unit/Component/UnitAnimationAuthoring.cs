using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class UnitAnimationAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitAnimationAuthoring>
    {
        public override void Bake(UnitAnimationAuthoring authoring)
        {
            Transform root = authoring.transform.root != null ? authoring.transform.root : authoring.transform;
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, UnitAnimationComponent.CreateDefault(new FixedString128Bytes(root.name)));
            AddComponent(entity, new UnitAnimationFrameUvMinProperty { Value = float4.zero });
            AddComponent(entity, new UnitAnimationFrameUvSizeProperty { Value = new float4(1f, 1f, 0f, 0f) });
            AddComponent(entity, new UnitAnimationFrameWorldSizeProperty { Value = new float4(1f, 1f, 0f, 0f) });
            AddComponent(entity, new UnitAnimationFramePivotOffsetProperty { Value = float4.zero });

            SpriteRenderer spriteRenderer = authoring.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                AddComponentObject(entity, spriteRenderer);
        }
    }
}
