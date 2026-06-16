using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitVisualFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitVisualFeatureAuthoring>
    {
        public override void Bake(UnitVisualFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitVisualComponents(authoring.transform, entity);
        }
    }
}
