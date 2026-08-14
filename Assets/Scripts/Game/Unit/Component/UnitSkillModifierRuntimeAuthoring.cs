using Unity.Entities;
using UnityEngine;

public sealed class UnitSkillModifierRuntimeAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitSkillModifierRuntimeAuthoring>
    {
        public override void Bake(UnitSkillModifierRuntimeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitSkillModifierRuntimeComponent());
        }
    }
}
