using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitJumpFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitJumpFeatureAuthoring>
    {
        public override void Bake(UnitJumpFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitJumpComponents(entity);
        }
    }
}
