using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitControlFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitControlFeatureAuthoring>
    {
        public override void Bake(UnitControlFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitControlComponents(entity);
        }
    }
}
