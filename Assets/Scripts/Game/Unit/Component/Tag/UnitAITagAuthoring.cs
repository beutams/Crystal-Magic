using Unity.Entities;
using UnityEngine;

public class UnitAITagAuthoring : MonoBehaviour
{
    class UnitAITagBaker : Baker<UnitAITagAuthoring>
    {
        public override void Bake(UnitAITagAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UnitAITag>(entity);
        }
    }
}

public struct UnitAITag : IComponentData
{
}
