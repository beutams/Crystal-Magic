using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitManaAuthoring : MonoBehaviour
{
    class UnitManaBaker : Baker<UnitManaAuthoring>
    {
        public override void Bake(UnitManaAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseMp = 50f;
            float baseMpRegenPerSecond = 0f;
            UnitManaModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitManaModuleData>(authoring);
            if (data != null)
            {
                baseMp = data.BaseMaxMp;
                baseMpRegenPerSecond = data.BaseMpRegenPerSecond;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitManaComponent
            {
                BaseMaxMp = baseMp,
                BaseMaxMpOffset = 0f,
                CurrentMana = baseMp,
                BaseMpRegenPerSecond = baseMpRegenPerSecond,
                BaseMpRegenPerSecondOffset = 0f,
            });
        }
    }
}

public struct UnitManaComponent : IComponentData
{
    public float BaseMaxMp;
    public float BaseMaxMpOffset;
    public float CurrentMana;
    public float BaseMpRegenPerSecond;
    public float BaseMpRegenPerSecondOffset;
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(UnitManaComponent), typeof(UnitManaAuthoring))]
public static class UnitManaSource
{
    [UnitSourceGet(0, "unit.mana.baseMaxMp", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.mana.baseMaxMpOffset", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.mana.currentMana", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.mana.baseMpRegenPerSecond", UnitValueCategory.Number)]
    [UnitSourceGet(4, "unit.mana.baseMpRegenPerSecondOffset", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitManaComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromFloat(value.BaseMaxMp),
            1 => UnitSourceValue.FromFloat(value.BaseMaxMpOffset),
            2 => UnitSourceValue.FromFloat(value.CurrentMana),
            3 => UnitSourceValue.FromFloat(value.BaseMpRegenPerSecond),
            4 => UnitSourceValue.FromFloat(value.BaseMpRegenPerSecondOffset),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(5, "unit.mana.realMaxMp", UnitValueCategory.Number)]
    [UnitSourceGet(6, "unit.mana.currentManaPercentage", UnitValueCategory.Number)]
    [UnitSourceGet(7, "unit.mana.realMpRegenPerSecond", UnitValueCategory.Number)]
    public static bool TryGetResolved(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = UnitSourceValue.None;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitManaComponent>(entity))
            return false;

        UnitManaComponent value = entityManager.GetComponentData<UnitManaComponent>(entity);
        switch (operation)
        {
            case 5:
                result = UnitSourceValue.FromFloat(UnitModifierResolver.GetMaxMp(entityManager, entity));
                return true;
            case 6:
                float maxMp = UnitModifierResolver.GetMaxMp(entityManager, entity);
                result = UnitSourceValue.FromFloat(maxMp > 0f ? Mathf.Clamp01(value.CurrentMana / maxMp) : 0f);
                return true;
            case 7:
                result = UnitSourceValue.FromFloat(UnitModifierResolver.GetMpRegen(entityManager, entity));
                return true;
            default:
                return false;
        }
    }

    [UnitSourceSet(0, "unit.mana.cost", UnitValueCategory.Number, ParameterNames = new[] { "Cost" })]
    public static bool TrySet(int operation, ref UnitManaComponent value, in UnitSourceArguments arguments)
    {
        if (operation != 0 || !arguments.TryGetNumber(0, out float cost) ||
            !math.isfinite(cost) || cost < 0f || value.CurrentMana < cost)
        {
            return false;
        }

        value.CurrentMana -= cost;
        value.NetworkDirty = 1;
        return true;
    }
}
