using CrystalMagic.Game.Data;
using Unity.Entities;

public static class UnitModifierUtility
{
    public static void ApplyEquipmentPropertyModifiers(EntityManager entityManager, Entity entity, PropertyModifierSet modifiers)
    {
        if (entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            float moveSpeedOffset = modifiers.GetBonus(PropertyModifierChannel.MoveSpeed);
            if (move.BaseMoveSpeedOffset != moveSpeedOffset)
            {
                move.BaseMoveSpeedOffset = moveSpeedOffset;
                move.NetworkDirty = 1;
                entityManager.SetComponentData(entity, move);
            }
        }

        if (entityManager.HasComponent<UnitVitalityComponent>(entity))
        {
            UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
            float maxHealthOffset = modifiers.GetBonus(PropertyModifierChannel.MaxHealth);
            float healthRegenOffset = modifiers.GetBonus(PropertyModifierChannel.HealthRegen);
            float defenseOffset = modifiers.GetBonus(PropertyModifierChannel.Defense);
            if (vitality.BaseMaxHealthOffset != maxHealthOffset ||
                vitality.BaseHealthRegenOffset != healthRegenOffset ||
                vitality.BaseDefenseOffset != defenseOffset)
            {
                vitality.BaseMaxHealthOffset = maxHealthOffset;
                vitality.BaseHealthRegenOffset = healthRegenOffset;
                vitality.BaseDefenseOffset = defenseOffset;
                vitality.NetworkDirty = 1;
                entityManager.SetComponentData(entity, vitality);
            }
        }

        if (entityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
            float maxMpOffset = modifiers.GetBonus(PropertyModifierChannel.MaxMp);
            float mpRegenOffset = modifiers.GetBonus(PropertyModifierChannel.MpRegen);
            if (mana.BaseMaxMpOffset != maxMpOffset || mana.BaseMpRegenPerSecondOffset != mpRegenOffset)
            {
                mana.BaseMaxMpOffset = maxMpOffset;
                mana.BaseMpRegenPerSecondOffset = mpRegenOffset;
                mana.NetworkDirty = 1;
                entityManager.SetComponentData(entity, mana);
            }
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
