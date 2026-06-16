using CrystalMagic.Game.Data;
using Unity.Entities;

public static class UnitModifierUtility
{
    public static void ResetFrameProperties(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return;

        UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(entity);
        element.WaterPower = 0f;
        element.FirePower = 0f;
        element.LightningPower = 0f;
        element.WindPower = 0f;
        entityManager.SetComponentData(entity, element);
    }

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
            attack.BaseActionSpeedBonusOffset = modifiers.GetBonus(PropertyModifierChannel.ActionSpeed);
            attack.BaseChantSpeedBonusOffset = modifiers.GetBonus(PropertyModifierChannel.ChantSpeed);
            entityManager.SetComponentData(entity, attack);
        }

        ApplyElementBonuses(entityManager, entity, modifiers);
    }

    public static void ApplyRuntimePropertyModifiers(EntityManager entityManager, Entity entity, PropertyModifierSet modifiers)
    {
        if (entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            move.SpeedFactor = modifiers.GetFactor(PropertyModifierChannel.MoveSpeed);
            move.SpeedBonus = modifiers.GetBonus(PropertyModifierChannel.MoveSpeed);
            entityManager.SetComponentData(entity, move);
        }

        if (entityManager.HasComponent<UnitVitalityComponent>(entity))
        {
            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
            vitality.HealthFactor = modifiers.GetFactor(PropertyModifierChannel.MaxHealth);
            vitality.HealthBonus = modifiers.GetBonus(PropertyModifierChannel.MaxHealth);
            vitality.HealthRegenFactor = modifiers.GetFactor(PropertyModifierChannel.HealthRegen);
            vitality.HealthRegenBonus = modifiers.GetBonus(PropertyModifierChannel.HealthRegen);
            vitality.DefenseFactor = modifiers.GetFactor(PropertyModifierChannel.Defense);
            vitality.DefenseBonus = modifiers.GetBonus(PropertyModifierChannel.Defense);
            entityManager.SetComponentData(entity, vitality);
        }

        if (entityManager.HasComponent<UnitAttackComponent>(entity))
        {
            UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
            attack.AttackFactor = modifiers.GetFactor(PropertyModifierChannel.AttackPower);
            attack.AttackBonus = modifiers.GetBonus(PropertyModifierChannel.AttackPower);
            attack.RangeFactor = modifiers.GetFactor(PropertyModifierChannel.SkillRange);
            attack.RangeBonus = modifiers.GetBonus(PropertyModifierChannel.SkillRange);
            attack.ActionSpeedFactor = modifiers.GetFactor(PropertyModifierChannel.ActionSpeed);
            attack.ActionSpeedBonus = modifiers.GetBonus(PropertyModifierChannel.ActionSpeed);
            attack.ChantSpeedFactor = modifiers.GetFactor(PropertyModifierChannel.ChantSpeed);
            attack.ChantSpeedBonus = modifiers.GetBonus(PropertyModifierChannel.ChantSpeed);
            entityManager.SetComponentData(entity, attack);
        }

        if (entityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
            mana.MpFactor = modifiers.GetFactor(PropertyModifierChannel.MaxMp);
            mana.MpBonus = modifiers.GetBonus(PropertyModifierChannel.MaxMp);
            mana.MpRegenFactor = modifiers.GetFactor(PropertyModifierChannel.MpRegen);
            mana.MpRegenBonus = modifiers.GetBonus(PropertyModifierChannel.MpRegen);
            entityManager.SetComponentData(entity, mana);
        }

        ApplyElementBonuses(entityManager, entity, modifiers);
    }

    private static void ApplyElementBonuses(EntityManager entityManager, Entity entity, PropertyModifierSet modifiers)
    {
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return;

        UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(entity);
        element.WaterPower += modifiers.GetBonus(PropertyModifierChannel.WaterPower);
        element.FirePower += modifiers.GetBonus(PropertyModifierChannel.FirePower);
        element.LightningPower += modifiers.GetBonus(PropertyModifierChannel.LightningPower);
        element.WindPower += modifiers.GetBonus(PropertyModifierChannel.WindPower);
        entityManager.SetComponentData(entity, element);
    }
}
