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

[UnitSourceAuthoring(typeof(UnitSkillModifierRuntimeAuthoring))]
public sealed class UnitSkillModifierRuntimeSource : UnitManagedComponentSource<UnitSkillModifierRuntimeComponent>
{
    private static readonly ComparatorParameterDefinition[] s_baseMpCostParameter =
    {
        new ComparatorParameterDefinition("BaseMpCost", UnitValueCategory.Number),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitSkillModifierRuntimeComponent> builder)
    {
        builder.AddGet("unit.skillModifier.getMpCost", UnitValueCategory.Number, s_baseMpCostParameter,
            (in UnitSkillModifierRuntimeComponent component, UnitValue[] input) =>
            {
                if (!input[0].TryGetNumber(out float baseMpCost))
                    return UnitValue.None;

                return UnitValue.FromInt(UnitSkillModifierUtility.GetModifiedMpCost(component?.Modifiers, baseMpCost));
            });
    }
}
