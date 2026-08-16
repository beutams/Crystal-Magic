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
    public float ActionDurationMultiplier => GetDurationMultiplier(RealActionSpeedBonus);
    public float ChantDurationMultiplier => GetDurationMultiplier(RealChantSpeedBonus);

    public static float GetDurationMultiplier(float speedBonus)
    {
        return speedBonus >= 0f ? 1f / (1f + speedBonus / 100f) : 1f - speedBonus / 100f;
    }
}

[UnitSourceAuthoring(typeof(UnitAttackAuthoring))]
public sealed class UnitAttackSource : UnitComponentSource<UnitAttackComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitAttackComponent> builder)
    {
        builder.AddGet("unit.attack.baseAttackPower", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseAttackPower));
        builder.AddGet("unit.attack.baseAttackPowerOffset", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseAttackPowerOffset));
        builder.AddGet("unit.attack.attackFactor", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.AttackFactor));
        builder.AddGet("unit.attack.attackBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.AttackBonus));
        builder.AddGet("unit.attack.realAttackPower", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealAttackPower));
        builder.AddGet("unit.attack.baseSkillRange", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseSkillRange));
        builder.AddGet("unit.attack.baseSkillRangeOffset", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseSkillRangeOffset));
        builder.AddGet("unit.attack.rangeFactor", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RangeFactor));
        builder.AddGet("unit.attack.rangeBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RangeBonus));
        builder.AddGet("unit.attack.realSkillRange", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealSkillRange));
        builder.AddGet("unit.attack.baseActionSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseActionSpeedBonus));
        builder.AddGet("unit.attack.baseActionSpeedBonusOffset", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseActionSpeedBonusOffset));
        builder.AddGet("unit.attack.actionSpeedFactor", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ActionSpeedFactor));
        builder.AddGet("unit.attack.actionSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ActionSpeedBonus));
        builder.AddGet("unit.attack.realActionSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealActionSpeedBonus));
        builder.AddGet("unit.attack.actionDurationMultiplier", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ActionDurationMultiplier));
        builder.AddGet("unit.attack.baseChantSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseChantSpeedBonus));
        builder.AddGet("unit.attack.baseChantSpeedBonusOffset", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseChantSpeedBonusOffset));
        builder.AddGet("unit.attack.chantSpeedFactor", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ChantSpeedFactor));
        builder.AddGet("unit.attack.chantSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ChantSpeedBonus));
        builder.AddGet("unit.attack.realChantSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealChantSpeedBonus));
        builder.AddGet("unit.attack.chantDurationMultiplier", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ChantDurationMultiplier));
    }
}
