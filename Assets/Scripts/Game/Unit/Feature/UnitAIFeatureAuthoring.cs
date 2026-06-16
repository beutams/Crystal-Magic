using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitAIFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitAIFeatureAuthoring>
    {
        public override void Bake(UnitAIFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitAIComponents(authoring, entity);
        }
    }
}
