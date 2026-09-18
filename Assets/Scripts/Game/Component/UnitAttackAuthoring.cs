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

[UnitSourceProvider(typeof(UnitAttackComponent), typeof(UnitAttackAuthoring))]
public static class UnitAttackSource
{
    [UnitSourceGet(0, "unit.attack.baseAttackPower", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.attack.baseSkillRange", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.attack.baseChantSpeedBonus", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitAttackComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromFloat(value.BaseAttackPowerValue),
            1 => UnitSourceValue.FromFloat(value.BaseSkillRangeValue),
            2 => UnitSourceValue.FromFloat(value.BaseChantSpeedBonusValue),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(3, "unit.attack.realAttackPower", UnitValueCategory.Number)]
    [UnitSourceGet(4, "unit.attack.realSkillRange", UnitValueCategory.Number)]
    [UnitSourceGet(5, "unit.attack.realChantSpeedBonus", UnitValueCategory.Number)]
    [UnitSourceGet(6, "unit.attack.chantDurationMultiplier", UnitValueCategory.Number)]
    public static bool TryGetResolved(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitAttackComponent>(entity))
        {
            result = default;
            return false;
        }

        result = operation switch
        {
            3 => UnitSourceValue.FromFloat(UnitModifierResolver.GetAttackPower(entityManager, entity)),
            4 => UnitSourceValue.FromFloat(UnitModifierResolver.GetSkillRange(entityManager, entity)),
            5 => UnitSourceValue.FromFloat(UnitModifierResolver.GetChantSpeedBonus(entityManager, entity)),
            6 => UnitSourceValue.FromFloat(UnitAttackComponent.GetDurationMultiplier(
                UnitModifierResolver.GetChantSpeedBonus(entityManager, entity))),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }
}
