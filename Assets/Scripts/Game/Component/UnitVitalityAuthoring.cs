using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitVitalityAuthoring : MonoBehaviour
{
    class UnitVitalityBaker : Baker<UnitVitalityAuthoring>
    {
        public override void Bake(UnitVitalityAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseHealth = 100f;
            float baseHealthRegenPerSecond = 0f;
            float baseDefense = 0f;
            UnitVitalityModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitVitalityModuleData>(authoring);
            if (data != null)
            {
                baseHealth = data.BaseMaxHealth;
                baseHealthRegenPerSecond = data.BaseHealthRegenPerSecond;
                baseDefense = data.BaseDefense;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitVitalityComponent
            {
                BaseMaxHealth = baseHealth,
                BaseMaxHealthOffset = 0f,
                CurrentHealth = baseHealth,
                BaseHealthRegenPerSecond = baseHealthRegenPerSecond,
                BaseHealthRegenOffset = 0f,
                BaseDefense = baseDefense,
                BaseDefenseOffset = 0f,
            });

        }
    }
}

public struct UnitVitalityComponent : IComponentData
{
    public float BaseMaxHealth;
    public float BaseMaxHealthOffset;
    public float CurrentHealth;
    public float BaseHealthRegenPerSecond;
    public float BaseHealthRegenOffset;
    public float BaseDefense;
    public float BaseDefenseOffset;
    public byte NetworkDirty;

    public float BaseMaxHealthValue => BaseMaxHealth + BaseMaxHealthOffset;
    public float BaseHealthRegenPerSecondValue => BaseHealthRegenPerSecond + BaseHealthRegenOffset;
    public float BaseDefenseValue => BaseDefense + BaseDefenseOffset;
}

[UnitSourceProvider(typeof(UnitVitalityComponent), typeof(UnitVitalityAuthoring))]
public static class UnitVitalitySource
{
    [UnitSourceGet(0, "unit.vitality.baseMaxHealth", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.vitality.currentHealth", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.vitality.baseHealthRegenPerSecond", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.vitality.baseDefense", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitVitalityComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromFloat(value.BaseMaxHealthValue),
            1 => UnitSourceValue.FromFloat(value.CurrentHealth),
            2 => UnitSourceValue.FromFloat(value.BaseHealthRegenPerSecondValue),
            3 => UnitSourceValue.FromFloat(value.BaseDefenseValue),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(4, "unit.vitality.realMaxHealth", UnitValueCategory.Number)]
    [UnitSourceGet(5, "unit.vitality.currentHealthPercentage", UnitValueCategory.Number)]
    [UnitSourceGet(6, "unit.vitality.realHealthRegenPerSecond", UnitValueCategory.Number)]
    [UnitSourceGet(7, "unit.vitality.realDefense", UnitValueCategory.Number)]
    public static bool TryGetResolved(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = UnitSourceValue.None;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitVitalityComponent>(entity))
            return false;

        switch (operation)
        {
            case 4:
                result = UnitSourceValue.FromFloat(UnitModifierResolver.GetMaxHealth(entityManager, entity));
                return true;
            case 5:
                float maxHealth = UnitModifierResolver.GetMaxHealth(entityManager, entity);
                float currentHealth = entityManager.GetComponentData<UnitVitalityComponent>(entity).CurrentHealth;
                result = UnitSourceValue.FromFloat(maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f);
                return true;
            case 6:
                result = UnitSourceValue.FromFloat(UnitModifierResolver.GetHealthRegen(entityManager, entity));
                return true;
            case 7:
                result = UnitSourceValue.FromFloat(UnitModifierResolver.GetDefense(entityManager, entity));
                return true;
            default:
                return false;
        }
    }
}
