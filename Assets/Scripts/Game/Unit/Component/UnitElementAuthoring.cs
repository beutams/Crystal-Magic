using Unity.Entities;
using UnityEngine;

public sealed class UnitElementAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitElementAuthoring>
    {
        public override void Bake(UnitElementAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitElementComponent
            {
                WaterPower = 0f,
                FirePower = 0f,
                LightningPower = 0f,
                WindPower = 0f,
            });
        }
    }
}
