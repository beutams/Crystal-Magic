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
                HealthFactor = 1f,
                HealthBonus = 0f,
                CurrentHealth = baseHealth,
                BaseHealthRegenPerSecond = baseHealthRegenPerSecond,
                BaseHealthRegenOffset = 0f,
                HealthRegenFactor = 1f,
                HealthRegenBonus = 0f,
                BaseDefense = baseDefense,
                BaseDefenseOffset = 0f,
                DefenseFactor = 1f,
                DefenseBonus = 0f,
            });

        }
    }
}

public struct UnitVitalityComponent : IComponentData
{
    public float BaseMaxHealth;
    public float BaseMaxHealthOffset;
    public float HealthFactor;
    public float HealthBonus;
    public float CurrentHealth;
    public float BaseHealthRegenPerSecond;
    public float BaseHealthRegenOffset;
    public float HealthRegenFactor;
    public float HealthRegenBonus;
    public float BaseDefense;
    public float BaseDefenseOffset;
    public float DefenseFactor;
    public float DefenseBonus;

    public float RealMaxHealth => (BaseMaxHealth + BaseMaxHealthOffset) * HealthFactor + HealthBonus;
    public float RealHealthRegenPerSecond => (BaseHealthRegenPerSecond + BaseHealthRegenOffset) * HealthRegenFactor + HealthRegenBonus;
    public float RealDefense => (BaseDefense + BaseDefenseOffset) * DefenseFactor + DefenseBonus;
}

[UnitSourceAuthoring(typeof(UnitVitalityAuthoring))]
public sealed class UnitVitalitySource : UnitComponentSource<UnitVitalityComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitVitalityComponent> builder)
    {
        builder.AddGet("unit.vitality.baseMaxHealth", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseMaxHealth));
        builder.AddGet("unit.vitality.baseMaxHealthOffset", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseMaxHealthOffset));
        builder.AddGet("unit.vitality.healthFactor", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.HealthFactor));
        builder.AddGet("unit.vitality.healthBonus", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.HealthBonus));
        builder.AddGet("unit.vitality.currentHealth", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.CurrentHealth));
        builder.AddGet("unit.vitality.realMaxHealth", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.RealMaxHealth));
        builder.AddGet("unit.vitality.currentHealthPercentage", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(GetHealthPercentage(value)));
        builder.AddGet("unit.vitality.baseHealthRegenPerSecond", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseHealthRegenPerSecond));
        builder.AddGet("unit.vitality.baseHealthRegenOffset", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseHealthRegenOffset));
        builder.AddGet("unit.vitality.healthRegenFactor", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.HealthRegenFactor));
        builder.AddGet("unit.vitality.healthRegenBonus", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.HealthRegenBonus));
        builder.AddGet("unit.vitality.realHealthRegenPerSecond", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.RealHealthRegenPerSecond));
        builder.AddGet("unit.vitality.baseDefense", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseDefense));
        builder.AddGet("unit.vitality.baseDefenseOffset", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.BaseDefenseOffset));
        builder.AddGet("unit.vitality.defenseFactor", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.DefenseFactor));
        builder.AddGet("unit.vitality.defenseBonus", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.DefenseBonus));
        builder.AddGet("unit.vitality.realDefense", UnitValueCategory.Number, (in UnitVitalityComponent value) => UnitValue.FromFloat(value.RealDefense));
    }

    private static float GetHealthPercentage(in UnitVitalityComponent value)
    {
        return value.RealMaxHealth > 0f ? Mathf.Clamp01(value.CurrentHealth / value.RealMaxHealth) : 0f;
    }
}
