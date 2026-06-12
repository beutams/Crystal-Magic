using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
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
            UnitAttackModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitAttackModuleData>(authoring);
            if (data != null)
            {
                baseAttack = data.BaseAttackPower;
                baseRange  = data.BaseSkillRange;
                baseActionSpeedBonus = data.BaseActionSpeedBonus;
                baseChantSpeedBonus = data.BaseChantSpeedBonus;
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
                WaterPower = 0f,
                FirePower = 0f,
                LightningPower = 0f,
                WindPower = 0f,
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
    public float RealActionSpeedBonus => math.clamp((BaseActionSpeedBonus + BaseActionSpeedBonusOffset) * ActionSpeedFactor + ActionSpeedBonus, -100f, 100f);
    public float RealChantSpeedBonus => math.clamp((BaseChantSpeedBonus + BaseChantSpeedBonusOffset) * ChantSpeedFactor + ChantSpeedBonus, -100f, 100f);
    public float ActionDurationMultiplier => RealActionSpeedBonus >= 0f
        ? 1f / (1f + RealActionSpeedBonus / 100f)
        : 1f - RealActionSpeedBonus / 100f;
    public float ChantDurationMultiplier => 1f - RealChantSpeedBonus / 100f;
}

public struct UnitElementComponent : IComponentData
{
    public float WaterPower;
    public float FirePower;
    public float LightningPower;
    public float WindPower;

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
