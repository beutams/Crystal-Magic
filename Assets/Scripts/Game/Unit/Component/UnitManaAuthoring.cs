using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitManaAuthoring : MonoBehaviour
{
    class UnitManaBaker : Baker<UnitManaAuthoring>
    {
        public override void Bake(UnitManaAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseMp = 50f;
            float baseMpRegenPerSecond = 0f;
            UnitManaModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitManaModuleData>(authoring);
            if (data != null)
            {
                baseMp = data.BaseMaxMp;
                baseMpRegenPerSecond = data.BaseMpRegenPerSecond;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitManaComponent
            {
                BaseMaxMp = baseMp,
                BaseMaxMpOffset = 0f,
                CurrentMana = baseMp,
                BaseMpRegenPerSecond = baseMpRegenPerSecond,
                BaseMpRegenPerSecondOffset = 0f,
            });
        }
    }
}

public struct UnitManaComponent : IComponentData
{
    public float BaseMaxMp;
    public float BaseMaxMpOffset;
    public float CurrentMana;
    public float BaseMpRegenPerSecond;
    public float BaseMpRegenPerSecondOffset;
}

[UnitSourceAuthoring(typeof(UnitManaAuthoring))]
public sealed class UnitManaSource : UnitComponentSource<UnitManaComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = System.Array.Empty<ComparatorParameterDefinition>();

    protected override void Define(UnitSourceDefinitionBuilder<UnitManaComponent> builder)
    {
        builder.AddGet("unit.mana.baseMaxMp", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMaxMp));
        builder.AddGet("unit.mana.baseMaxMpOffset", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMaxMpOffset));
        builder.AddGet("unit.mana.currentMana", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.CurrentMana));
        builder.AddContextGet("unit.mana.realMaxMp", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitManaComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetMaxMp(context.EntityManager, context.Entity)));
        builder.AddContextGet("unit.mana.currentManaPercentage", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitManaComponent value, UnitValue[] _) => UnitValue.FromFloat(GetManaPercentage(context, value)));
        builder.AddGet("unit.mana.baseMpRegenPerSecond", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMpRegenPerSecond));
        builder.AddGet("unit.mana.baseMpRegenPerSecondOffset", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMpRegenPerSecondOffset));
        builder.AddContextGet("unit.mana.realMpRegenPerSecond", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitManaComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetMpRegen(context.EntityManager, context.Entity)));

        builder.AddSet("unit.mana.cost", UnitValueCategory.Number,
            (ref UnitManaComponent value, UnitValue input) =>
            {
                if (!input.TryGetNumber(out float cost) ||
                    float.IsNaN(cost) ||
                    float.IsInfinity(cost) ||
                    cost < 0f ||
                    value.CurrentMana < cost)
                {
                    return false;
                }

                value.CurrentMana -= cost;
                return true;
            });
    }

    private static float GetManaPercentage(in UnitSourceBindingContext context, in UnitManaComponent value)
    {
        float maxMp = UnitModifierResolver.GetMaxMp(context.EntityManager, context.Entity);
        return maxMp > 0f ? Mathf.Clamp01(value.CurrentMana / maxMp) : 0f;
    }
}
