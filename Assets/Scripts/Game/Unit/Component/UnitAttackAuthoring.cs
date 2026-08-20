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
            float baseChantSpeedBonus = 0f;
            UnitAttackModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitAttackModuleData>(authoring);
            if (data != null)
            {
                baseAttack = data.BaseAttackPower;
                baseRange  = data.BaseSkillRange;
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

    public float BaseChantSpeedBonus;
    public float BaseChantSpeedBonusOffset;
    public float ChantSpeedFactor;
    public float ChantSpeedBonus;

    public float BaseAttackPowerValue => BaseAttackPower + BaseAttackPowerOffset;
    public float BaseSkillRangeValue => BaseSkillRange + BaseSkillRangeOffset;
    public float BaseChantSpeedBonusValue => BaseChantSpeedBonus + BaseChantSpeedBonusOffset;
    public float RealAttackPower => BaseAttackPowerValue * AttackFactor + AttackBonus;
    public float RealSkillRange => BaseSkillRangeValue * RangeFactor + RangeBonus;
    public float RealChantSpeedBonus => math.clamp(BaseChantSpeedBonusValue * ChantSpeedFactor + ChantSpeedBonus, -100f, 100f);
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
        builder.AddGet("unit.attack.baseAttackPower", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseAttackPowerValue));
        builder.AddGet("unit.attack.realAttackPower", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealAttackPower));
        builder.AddGet("unit.attack.baseSkillRange", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseSkillRangeValue));
        builder.AddGet("unit.attack.realSkillRange", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealSkillRange));
        builder.AddGet("unit.attack.baseChantSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseChantSpeedBonusValue));
        builder.AddGet("unit.attack.realChantSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.RealChantSpeedBonus));
        builder.AddGet("unit.attack.chantDurationMultiplier", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.ChantDurationMultiplier));
    }
}
