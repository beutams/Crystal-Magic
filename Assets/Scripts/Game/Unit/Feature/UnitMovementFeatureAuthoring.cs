using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitMovementFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitMovementFeatureAuthoring>
    {
        public override void Bake(UnitMovementFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitMovementComponents(authoring, entity);
        }
    }
}
