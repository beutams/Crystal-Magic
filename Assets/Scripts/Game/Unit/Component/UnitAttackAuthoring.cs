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
                WaterPower = baseWaterPowerBonus,
                EquipmentWaterPower = 0f,
                FirePower = baseFirePowerBonus,
                EquipmentFirePower = 0f,
                LightningPower = baseLightningPowerBonus,
                EquipmentLightningPower = 0f,
                WindPower = baseWindPowerBonus,
                EquipmentWindPower = 0f,
            });
            AddComponent(entity, new UnitElementBaseComponent
            {
                WaterPower = baseWaterPowerBonus,
                FirePower = baseFirePowerBonus,
                LightningPower = baseLightningPowerBonus,
                WindPower = baseWindPowerBonus,
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
    public float WaterPower;
    public float EquipmentWaterPower;
    public float FirePower;
    public float EquipmentFirePower;
    public float LightningPower;
    public float EquipmentLightningPower;
    public float WindPower;
    public float EquipmentWindPower;

    public float GetPowerBonus(CrystalMagic.Game.Data.Effects.ElementType elementType)
    {
        return elementType switch
        {
            CrystalMagic.Game.Data.Effects.ElementType.Water => WaterPower,
            CrystalMagic.Game.Data.Effects.ElementType.Fire => FirePower,
            CrystalMagic.Game.Data.Effects.ElementType.Lightning => LightningPower,
            CrystalMagic.Game.Data.Effects.ElementType.Wind => WindPower,
            _ => 0f,
        };
    }
}

public struct UnitElementBaseComponent : IComponentData
{
    public float WaterPower;
    public float FirePower;
    public float LightningPower;
    public float WindPower;
}
