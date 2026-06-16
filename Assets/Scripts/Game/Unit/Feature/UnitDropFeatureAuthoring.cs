using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitDropFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitDropFeatureAuthoring>
    {
        public override void Bake(UnitDropFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitDropComponents(authoring, entity);
        }
    }
}
