using Unity.Entities;
using UnityEngine;

public sealed class UnitInitializationPendingAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitInitializationPendingAuthoring>
    {
        public override void Bake(UnitInitializationPendingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UnitInitializationPendingTag>(entity);
        }
    }
}

public struct UnitInitializationPendingTag : IComponentData
{
}
