using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitVitalityAuthoring : MonoBehaviour
{
    class UnitVitalityBaker : Baker<UnitVitalityAuthoring>
    {
        public override void Bake(UnitVitalityAuthoring authoring)
        {
            float baseHealth  = 100f;
            float baseDefense = 0f;
            UnitData data = UnitAuthoringUtility.ResolveUnitData(authoring);
            if (data != null)
            {
                baseHealth  = data.BaseMaxHealth;
                baseDefense = data.BaseDefense;
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
    public float RealDefense => BaseDefense * DefenseFactor + DefenseBonus;
}

/// <summary>
/// 鐢熷懡 + 闃插尽缁勪欢鈥斺€旀湁姝ょ粍浠跺嵆涓哄彲鍙楀嚮鍗曚綅銆?
/// </summary>
