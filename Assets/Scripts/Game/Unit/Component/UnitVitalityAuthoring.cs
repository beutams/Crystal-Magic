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
                BaseHealthRegenPerSecondOffset = 0f,
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
    public float BaseHealthRegenPerSecondOffset;
    public float HealthRegenFactor;
    public float HealthRegenBonus;
    public float BaseDefense;
    public float BaseDefenseOffset;
    public float DefenseFactor;
    public float DefenseBonus;

    public float RealMaxHealth => (BaseMaxHealth + BaseMaxHealthOffset) * HealthFactor + HealthBonus;
    public float RealHealthRegenPerSecond => (BaseHealthRegenPerSecond + BaseHealthRegenPerSecondOffset) * HealthRegenFactor + HealthRegenBonus;
    public float RealDefense => (BaseDefense + BaseDefenseOffset) * DefenseFactor + DefenseBonus;
}
