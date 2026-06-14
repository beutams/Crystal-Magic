using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitJumpArcAuthoring : MonoBehaviour
{
    private sealed class UnitJumpArcBaker : Baker<UnitJumpArcAuthoring>
    {
        public override void Bake(UnitJumpArcAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitJumpArcComponent
            {
                StartPosition = float3.zero,
                EndPosition = float3.zero,
                Duration = 0f,
                Elapsed = 0f,
                ArcHeight = 0f,
                IsActive = 0,
                IsCompleted = 1,
            });
        }
    }
}
