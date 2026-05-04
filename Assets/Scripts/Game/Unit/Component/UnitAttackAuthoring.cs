using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitAttackAuthoring : MonoBehaviour
{
    class UnitAttackBaker : Baker<UnitAttackAuthoring>
    {
        public override void Bake(UnitAttackAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseAttack = 10f;
            float baseRange  = 1f;
            float baseActionSpeedBonus = 0f;
            float baseChantSpeedBonus = 0f;
            float baseWaterPowerBonus = 0f;
            float baseFirePowerBonus = 0f;
            float baseLightningPowerBonus = 0f;
            float baseWindPowerBonus = 0f;
            UnitAttackModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitAttackModuleData>(authoring);
            if (data != null)
            {
                baseAttack = data.BaseAttackPower;
                baseRange  = data.BaseSkillRange;
                baseActionSpeedBonus = data.BaseActionSpeedBonus;
                baseChantSpeedBonus = data.BaseChantSpeedBonus;
                baseWaterPowerBonus = data.BaseWaterPowerBonus;
                baseFirePowerBonus = data.BaseFirePowerBonus;
                baseLightningPowerBonus = data.BaseLightningPowerBonus;
                baseWindPowerBonus = data.BaseWindPowerBonus;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitAttackComponent
            {
                BaseAttackPower = baseAttack,
                AttackFactor    = 1f,
                AttackBonus     = 0f,
                BaseSkillRange  = baseRange,
                RangeFactor     = 1f,
                RangeBonus      = 0f,
                BaseActionSpeedBonus = baseActionSpeedBonus,
                ActionSpeedFactor = 1f,
                ActionSpeedBonus = 0f,
                BaseChantSpeedBonus = baseChantSpeedBonus,
                ChantSpeedFactor = 1f,
                ChantSpeedBonus = 0f,
            });
            AddComponent(entity, new UnitElementComponent
            {
                BaseWaterPowerBonus = baseWaterPowerBonus,
                WaterPowerFactor = 1f,
                WaterPowerBonus = 0f,
                BaseFirePowerBonus = baseFirePowerBonus,
                FirePowerFactor = 1f,
                FirePowerBonus = 0f,
                BaseLightningPowerBonus = baseLightningPowerBonus,
                LightningPowerFactor = 1f,
                LightningPowerBonus = 0f,
                BaseWindPowerBonus = baseWindPowerBonus,
                WindPowerFactor = 1f,
                WindPowerBonus = 0f,
            });
        }
    }
}

public struct UnitAttackComponent : IComponentData
{
    public float BaseAttackPower;
    public float AttackFactor;
    public float AttackBonus;
    public float BaseSkillRange;
    public float RangeFactor;
    public float RangeBonus;
    public float BaseActionSpeedBonus;
    public float ActionSpeedFactor;
    public float ActionSpeedBonus;
    public float BaseChantSpeedBonus;
    public float ChantSpeedFactor;
    public float ChantSpeedBonus;

    public float RealAttackPower => BaseAttackPower * AttackFactor + AttackBonus;
    public float RealSkillRange => BaseSkillRange * RangeFactor + RangeBonus;
    public float RealActionSpeedBonus => BaseActionSpeedBonus * ActionSpeedFactor + ActionSpeedBonus;
    public float RealChantSpeedBonus => BaseChantSpeedBonus * ChantSpeedFactor + ChantSpeedBonus;
}

public struct UnitElementComponent : IComponentData
{
    public float BaseWaterPowerBonus;
    public float WaterPowerFactor;
    public float WaterPowerBonus;
    public float BaseFirePowerBonus;
    public float FirePowerFactor;
    public float FirePowerBonus;
    public float BaseLightningPowerBonus;
    public float LightningPowerFactor;
    public float LightningPowerBonus;
    public float BaseWindPowerBonus;
    public float WindPowerFactor;
    public float WindPowerBonus;

    public float RealWaterPowerBonus => BaseWaterPowerBonus * WaterPowerFactor + WaterPowerBonus;
    public float RealFirePowerBonus => BaseFirePowerBonus * FirePowerFactor + FirePowerBonus;
    public float RealLightningPowerBonus => BaseLightningPowerBonus * LightningPowerFactor + LightningPowerBonus;
    public float RealWindPowerBonus => BaseWindPowerBonus * WindPowerFactor + WindPowerBonus;

    public float GetPowerBonus(CrystalMagic.Game.Data.Effects.ElementType elementType)
    {
        return elementType switch
        {
            CrystalMagic.Game.Data.Effects.ElementType.Water => RealWaterPowerBonus,
            CrystalMagic.Game.Data.Effects.ElementType.Fire => RealFirePowerBonus,
            CrystalMagic.Game.Data.Effects.ElementType.Lightning => RealLightningPowerBonus,
            CrystalMagic.Game.Data.Effects.ElementType.Wind => RealWindPowerBonus,
            _ => 0f,
        };
    }
}

/// <summary>
/// 攻击组件——有此组件即为可攻击单位。
/// </summary>
