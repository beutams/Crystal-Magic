using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitBattleFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitBattleFeatureAuthoring>
    {
        public override void Bake(UnitBattleFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitBattleComponents(authoring, entity);
        }
    }
}
