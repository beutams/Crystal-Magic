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
                BaseSkillRange  = baseRange,
                BaseSkillRangeOffset = 0f,
                BaseChantSpeedBonus = baseChantSpeedBonus,
                BaseChantSpeedBonusOffset = 0f,
            });
        }
    }
}

public struct UnitAttackComponent : IComponentData
{
    public float BaseAttackPower;
    public float BaseAttackPowerOffset;

    public float BaseSkillRange;
    public float BaseSkillRangeOffset;

    public float BaseChantSpeedBonus;
    public float BaseChantSpeedBonusOffset;

    public float BaseAttackPowerValue => BaseAttackPower + BaseAttackPowerOffset;
    public float BaseSkillRangeValue => BaseSkillRange + BaseSkillRangeOffset;
    public float BaseChantSpeedBonusValue => BaseChantSpeedBonus + BaseChantSpeedBonusOffset;

    public static float GetDurationMultiplier(float speedBonus)
    {
        return speedBonus >= 0f ? 1f / (1f + speedBonus / 100f) : 1f - speedBonus / 100f;
    }
}

[UnitSourceAuthoring(typeof(UnitAttackAuthoring))]
public sealed class UnitAttackSource : UnitComponentSource<UnitAttackComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = System.Array.Empty<ComparatorParameterDefinition>();

    protected override void Define(UnitSourceDefinitionBuilder<UnitAttackComponent> builder)
    {
        builder.AddGet("unit.attack.baseAttackPower", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseAttackPowerValue));
        builder.AddContextGet("unit.attack.realAttackPower", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitAttackComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetAttackPower(context.EntityManager, context.Entity)));
        builder.AddGet("unit.attack.baseSkillRange", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseSkillRangeValue));
        builder.AddContextGet("unit.attack.realSkillRange", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitAttackComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetSkillRange(context.EntityManager, context.Entity)));
        builder.AddGet("unit.attack.baseChantSpeedBonus", UnitValueCategory.Number, (in UnitAttackComponent value) => UnitValue.FromFloat(value.BaseChantSpeedBonusValue));
        builder.AddContextGet("unit.attack.realChantSpeedBonus", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitAttackComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetChantSpeedBonus(context.EntityManager, context.Entity)));
        builder.AddContextGet("unit.attack.chantDurationMultiplier", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitAttackComponent _, UnitValue[] _) =>
                UnitValue.FromFloat(UnitAttackComponent.GetDurationMultiplier(UnitModifierResolver.GetChantSpeedBonus(context.EntityManager, context.Entity))));
    }
}
