using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitDecisionFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitDecisionFeatureAuthoring>
    {
        public override void Bake(UnitDecisionFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitDecisionComponents(authoring, entity);
        }
    }
}
