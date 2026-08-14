using Unity.Entities;
using UnityEngine;

public sealed class DestroyEntityFlagAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<DestroyEntityFlagAuthoring>
    {
        public override void Bake(DestroyEntityFlagAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<DestroyEntityFlag>(entity);
            SetComponentEnabled<DestroyEntityFlag>(entity, false);
        }
    }
}
