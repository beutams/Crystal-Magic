using CrystalMagic.Core;
using Unity.Entities;

public static class UnitModifierUtility
{
    public static void ApplyEquipmentProperties(
        EntityManager entityManager,
        Entity entity,
        in EquipmentPropertyData properties)
    {
        if (entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            if (move.BaseMoveSpeedOffset != properties.MoveSpeed)
            {
                move.BaseMoveSpeedOffset = properties.MoveSpeed;
                move.NetworkDirty = 1;
                entityManager.SetComponentData(entity, move);
            }
        }

        if (entityManager.HasComponent<UnitVitalityComponent>(entity))
        {
            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
            if (vitality.BaseMaxHealthOffset != properties.MaxHealth ||
                vitality.BaseHealthRegenOffset != properties.HealthRegen ||
                vitality.BaseDefenseOffset != properties.Defense)
            {
                vitality.BaseMaxHealthOffset = properties.MaxHealth;
                vitality.BaseHealthRegenOffset = properties.HealthRegen;
                vitality.BaseDefenseOffset = properties.Defense;
                vitality.NetworkDirty = 1;
                entityManager.SetComponentData(entity, vitality);
            }
        }

        if (entityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
            if (mana.BaseMaxMpOffset != properties.MaxMp ||
                mana.BaseMpRegenPerSecondOffset != properties.MpRegen)
            {
                mana.BaseMaxMpOffset = properties.MaxMp;
                mana.BaseMpRegenPerSecondOffset = properties.MpRegen;
                mana.NetworkDirty = 1;
                entityManager.SetComponentData(entity, mana);
            }
        }

        if (entityManager.HasComponent<UnitAttackComponent>(entity))
        {
            UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
            if (attack.BaseAttackPowerOffset != properties.AttackPower ||
                attack.BaseSkillRangeOffset != properties.SkillRange ||
                attack.BaseChantSpeedBonusOffset != properties.ChantSpeed)
            {
                attack.BaseAttackPowerOffset = properties.AttackPower;
                attack.BaseSkillRangeOffset = properties.SkillRange;
                attack.BaseChantSpeedBonusOffset = properties.ChantSpeed;
                entityManager.SetComponentData(entity, attack);
            }
        }

        ApplyElementBonuses(entityManager, entity, in properties);
    }

    private static void ApplyElementBonuses(
        EntityManager entityManager,
        Entity entity,
        in EquipmentPropertyData properties)
    {
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return;

        UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(entity);
        if (element.WaterPower != properties.WaterPower ||
            element.FirePower != properties.FirePower ||
            element.LightningPower != properties.LightningPower ||
            element.WindPower != properties.WindPower)
        {
            element.WaterPower = properties.WaterPower;
            element.FirePower = properties.FirePower;
            element.LightningPower = properties.LightningPower;
            element.WindPower = properties.WindPower;
            entityManager.SetComponentData(entity, element);
        }
    }
}
