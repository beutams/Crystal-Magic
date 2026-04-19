using Unity.Entities;
using UnityEngine;


public class NPCTagAuthoring : MonoBehaviour
{
    class NPCTagBaker : Baker<NPCTagAuthoring>
    {
        public override void Bake(NPCTagAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<NPCTag>(entity);
        }
    }
}

public struct NPCTag : IComponentData
{
}
