using CrystalMagic.Game.Data;
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

public sealed class UnitSkillModifierRuntimeComponent : IComponentData
{
    public SkillModifierSet Modifiers = new();
}
