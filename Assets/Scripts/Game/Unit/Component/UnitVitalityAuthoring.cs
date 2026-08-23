using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitVitalityAuthoring : MonoBehaviour
{
    class UnitVitalityBaker : Baker<UnitVitalityAuthoring>
    {
        public override void Bake(UnitVitalityAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseHealth = 100f;
            float baseHealthRegenPerSecond = 0f;
            float baseDefense = 0f;
            UnitVitalityModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitVitalityModuleData>(authoring);
            if (data != null)
            {
                baseHealth = data.BaseMaxHealth;
                baseHealthRegenPerSecond = data.BaseHealthRegenPerSecond;
                baseDefense = data.BaseDefense;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitVitalityComponent
            {
                BaseMaxHealth = baseHealth,
                BaseMaxHealthOffset = 0f,
                CurrentHealth = baseHealth,
                BaseHealthRegenPerSecond = baseHealthRegenPerSecond,
                BaseHealthRegenOffset = 0f,
                BaseDefense = baseDefense,
                BaseDefenseOffset = 0f,
            });

        }
    }
}

public struct UnitVitalityComponent : IComponentData
{
    public float BaseMaxHealth;
    public float BaseMaxHealthOffset;
    public float CurrentHealth;
    public float BaseHealthRegenPerSecond;
    public float BaseHealthRegenOffset;
    public float BaseDefense;
    public float BaseDefenseOffset;

    public float BaseMaxHealthValue => BaseMaxHealth + BaseMaxHealthOffset;
    public float BaseHealthRegenPerSecondValue => BaseHealthRegenPerSecond + BaseHealthRegenOffset;
    public float BaseDefenseValue => BaseDefense + BaseDefenseOffset;
}

[UnitSourceAuthoring(typeof(UnitVitalityAuthoring))]
public sealed class UnitVitalitySource : UnitComponentSource<UnitVitalityComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = System.Array.Empty<ComparatorParameterDefinition>();

    protected override void Define(UnitSourceDefinitionBuilder<UnitVitalityComponent> builder)
    {
        builder.AddGet("unit.vitality.baseMaxHealth", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseMaxHealthValue));
        builder.AddGet("unit.vitality.currentHealth", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.CurrentHealth));
        builder.AddContextGet("unit.vitality.realMaxHealth", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitVitalityComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetMaxHealth(context.EntityManager, context.Entity)));
        builder.AddContextGet("unit.vitality.currentHealthPercentage", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitVitalityComponent value, UnitValue[] _) => UnitValue.FromFloat(GetHealthPercentage(context, value)));
        builder.AddGet("unit.vitality.baseHealthRegenPerSecond", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseHealthRegenPerSecondValue));
        builder.AddContextGet("unit.vitality.realHealthRegenPerSecond", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitVitalityComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetHealthRegen(context.EntityManager, context.Entity)));
        builder.AddGet("unit.vitality.baseDefense", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseDefenseValue));
        builder.AddContextGet("unit.vitality.realDefense", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitVitalityComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetDefense(context.EntityManager, context.Entity)));
    }

    private static float GetHealthPercentage(in UnitSourceBindingContext context, in UnitVitalityComponent value)
    {
        float maxHealth = UnitModifierResolver.GetMaxHealth(context.EntityManager, context.Entity);
        return maxHealth > 0f ? Mathf.Clamp01(value.CurrentHealth / maxHealth) : 0f;
    }
}
