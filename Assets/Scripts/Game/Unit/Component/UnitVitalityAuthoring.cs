using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitVitalityAuthoring : MonoBehaviour
{
    public string UnitName;

    class UnitVitalityBaker : Baker<UnitVitalityAuthoring>
    {
        public override void Bake(UnitVitalityAuthoring authoring)
        {
            float baseHealth  = 100f;
            float baseDefense = 0f;
            if (!string.IsNullOrEmpty(authoring.UnitName))
            {
                UnitData data = EditorComponents.Data.Find<UnitData>(r => r.Name == authoring.UnitName);
                if (data != null)
                {
                    baseHealth  = data.BaseMaxHealth;
                    baseDefense = data.BaseDefense;
                }
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitVitalityComponent
            {
                BaseMaxHealth  = baseHealth,
                HealthFactor   = 1f,
                HealthBonus    = 0f,
                CurrentHealth  = baseHealth,
                BaseDefense    = baseDefense,
                DefenseFactor  = 1f,
                DefenseBonus   = 0f,
            });
        }
    }
}

/// <summary>
/// 生命 + 防御组件——有此组件即为可受击单位。
/// </summary>
public struct UnitVitalityComponent : IComponentData
{
    public float BaseMaxHealth;
    public float HealthFactor;
    public float HealthBonus;
    public float CurrentHealth;

    public float BaseDefense;
    public float DefenseFactor;
    public float DefenseBonus;

    public float RealMaxHealth => BaseMaxHealth * HealthFactor + HealthBonus;
    public float RealDefense   => BaseDefense   * DefenseFactor + DefenseBonus;
}
