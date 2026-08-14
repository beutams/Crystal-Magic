using Unity.Entities;
using UnityEngine;

public sealed class UnitDeathAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitDeathAuthoring>
    {
        public override void Bake(UnitDeathAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UnitDeathComponent>(entity);
            SetComponentEnabled<UnitDeathComponent>(entity, false);
        }
    }
}
