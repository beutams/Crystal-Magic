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
                BaseAttackPowerOffset = 0f,
                AttackFactor    = 1f,
                AttackBonus     = 0f,
                BaseSkillRange  = baseRange,
                BaseSkillRangeOffset = 0f,
                RangeFactor     = 1f,
                RangeBonus      = 0f,
                BaseActionSpeedBonus = baseActionSpeedBonus,
                BaseActionSpeedBonusOffset = 0f,
                ActionSpeedFactor = 1f,
                ActionSpeedBonus = 0f,
                BaseChantSpeedBonus = baseChantSpeedBonus,
                BaseChantSpeedBonusOffset = 0f,
                ChantSpeedFactor = 1f,
                ChantSpeedBonus = 0f,
            });
            AddComponent(entity, new UnitElementComponent
            {
                BaseWaterPowerBonus = baseWaterPowerBonus,
                BaseWaterPowerBonusOffset = 0f,
                WaterPowerFactor = 1f,
                WaterPowerBonus = 0f,
                BaseFirePowerBonus = baseFirePowerBonus,
                BaseFirePowerBonusOffset = 0f,
                FirePowerFactor = 1f,
                FirePowerBonus = 0f,
                BaseLightningPowerBonus = baseLightningPowerBonus,
                BaseLightningPowerBonusOffset = 0f,
                LightningPowerFactor = 1f,
                LightningPowerBonus = 0f,
                BaseWindPowerBonus = baseWindPowerBonus,
                BaseWindPowerBonusOffset = 0f,
                WindPowerFactor = 1f,
                WindPowerBonus = 0f,
            });
        }
    }
}

public struct UnitAttackComponent : IComponentData
{
    public float BaseAttackPower;
    public float BaseAttackPowerOffset;
    public float AttackFactor;
    public float AttackBonus;
    public float BaseSkillRange;
    public float BaseSkillRangeOffset;
    public float RangeFactor;
    public float RangeBonus;
    public float BaseActionSpeedBonus;
    public float BaseActionSpeedBonusOffset;
    public float ActionSpeedFactor;
    public float ActionSpeedBonus;
    public float BaseChantSpeedBonus;
    public float BaseChantSpeedBonusOffset;
    public float ChantSpeedFactor;
    public float ChantSpeedBonus;

    public float RealAttackPower => (BaseAttackPower + BaseAttackPowerOffset) * AttackFactor + AttackBonus;
    public float RealSkillRange => (BaseSkillRange + BaseSkillRangeOffset) * RangeFactor + RangeBonus;
    public float RealActionSpeedBonus => (BaseActionSpeedBonus + BaseActionSpeedBonusOffset) * ActionSpeedFactor + ActionSpeedBonus;
    public float RealChantSpeedBonus => (BaseChantSpeedBonus + BaseChantSpeedBonusOffset) * ChantSpeedFactor + ChantSpeedBonus;
}

public struct UnitElementComponent : IComponentData
{
    public float BaseWaterPowerBonus;
    public float BaseWaterPowerBonusOffset;
    public float WaterPowerFactor;
    public float WaterPowerBonus;
    public float BaseFirePowerBonus;
    public float BaseFirePowerBonusOffset;
    public float FirePowerFactor;
    public float FirePowerBonus;
    public float BaseLightningPowerBonus;
    public float BaseLightningPowerBonusOffset;
    public float LightningPowerFactor;
    public float LightningPowerBonus;
    public float BaseWindPowerBonus;
    public float BaseWindPowerBonusOffset;
    public float WindPowerFactor;
    public float WindPowerBonus;

    public float RealWaterPowerBonus => (BaseWaterPowerBonus + BaseWaterPowerBonusOffset) * WaterPowerFactor + WaterPowerBonus;
    public float RealFirePowerBonus => (BaseFirePowerBonus + BaseFirePowerBonusOffset) * FirePowerFactor + FirePowerBonus;
    public float RealLightningPowerBonus => (BaseLightningPowerBonus + BaseLightningPowerBonusOffset) * LightningPowerFactor + LightningPowerBonus;
    public float RealWindPowerBonus => (BaseWindPowerBonus + BaseWindPowerBonusOffset) * WindPowerFactor + WindPowerBonus;

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
