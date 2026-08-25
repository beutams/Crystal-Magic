using CrystalMagic.Game.Data;
using Unity.Entities;

public static class UnitModifierUtility
{
    public static void ApplyEquipmentPropertyModifiers(EntityManager entityManager, Entity entity, PropertyModifierSet modifiers)
    {
        if (entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            move.BaseMoveSpeedOffset = modifiers.GetBonus(PropertyModifierChannel.MoveSpeed);
            entityManager.SetComponentData(entity, move);
        }

        if (entityManager.HasComponent<UnitVitalityComponent>(entity))
        {
            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
            vitality.BaseMaxHealthOffset = modifiers.GetBonus(PropertyModifierChannel.MaxHealth);
            vitality.BaseHealthRegenOffset = modifiers.GetBonus(PropertyModifierChannel.HealthRegen);
            vitality.BaseDefenseOffset = modifiers.GetBonus(PropertyModifierChannel.Defense);
            entityManager.SetComponentData(entity, vitality);
        }

        if (entityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
            mana.BaseMaxMpOffset = modifiers.GetBonus(PropertyModifierChannel.MaxMp);
            mana.BaseMpRegenPerSecondOffset = modifiers.GetBonus(PropertyModifierChannel.MpRegen);
            entityManager.SetComponentData(entity, mana);
        }

        if (entityManager.HasComponent<UnitAttackComponent>(entity))
        {
            UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
            attack.BaseAttackPowerOffset = modifiers.GetBonus(PropertyModifierChannel.AttackPower);
            attack.BaseSkillRangeOffset = modifiers.GetBonus(PropertyModifierChannel.SkillRange);
            attack.BaseChantSpeedBonusOffset = modifiers.GetBonus(PropertyModifierChannel.ChantSpeed);
            entityManager.SetComponentData(entity, attack);
        }

        ApplyElementBonuses(entityManager, entity, modifiers);
    }

    private static void ApplyElementBonuses(EntityManager entityManager, Entity entity, PropertyModifierSet modifiers)
    {
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return;

        UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(entity);
        element.WaterPower = modifiers.GetBonus(PropertyModifierChannel.WaterPower);
        element.FirePower = modifiers.GetBonus(PropertyModifierChannel.FirePower);
        element.LightningPower = modifiers.GetBonus(PropertyModifierChannel.LightningPower);
        element.WindPower = modifiers.GetBonus(PropertyModifierChannel.WindPower);
        entityManager.SetComponentData(entity, element);
    }
}
