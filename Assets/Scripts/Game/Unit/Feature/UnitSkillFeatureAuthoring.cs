using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitSkillFeatureAuthoring : MonoBehaviour
{
    private sealed class Baker : Unity.Entities.Baker<UnitSkillFeatureAuthoring>
    {
        public override void Bake(UnitSkillFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddUnitSkillComponents(authoring, entity);
        }
    }
}
