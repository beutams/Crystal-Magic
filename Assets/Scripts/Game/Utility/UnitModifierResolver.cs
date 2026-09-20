using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;

public static class UnitModifierResolver
{
    public static float GetMoveSpeed(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitMoveComponent>(entity))
            return 0f;

        UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetMoveSpeed(in move, in modifiers);
    }

    public static float GetMoveSpeed(in UnitMoveComponent move, in UnitModifierComponent modifiers) =>
        modifiers.MoveSpeed.Apply(move.BaseMoveSpeedValue);

    public static float GetMaxAcceleration(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitMoveComponent>(entity))
            return 0f;

        UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetMaxAcceleration(in move, in modifiers);
    }

    public static float GetMaxAcceleration(in UnitMoveComponent move, in UnitModifierComponent modifiers) =>
        modifiers.MoveSpeed.Apply(move.BaseMaxAcceleration);

    public static float GetMaxHealth(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitVitalityComponent>(entity))
            return 0f;

        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetMaxHealth(in vitality, in modifiers);
    }

    public static float GetMaxHealth(in UnitVitalityComponent vitality, in UnitModifierComponent modifiers) =>
        modifiers.MaxHealth.Apply(vitality.BaseMaxHealthValue);

    public static float GetDefense(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitVitalityComponent>(entity))
            return 0f;

        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetDefense(in vitality, in modifiers);
    }

    public static float GetDefense(in UnitVitalityComponent vitality, in UnitModifierComponent modifiers) =>
        modifiers.Defense.Apply(vitality.BaseDefenseValue);

    public static float GetAttackPower(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitAttackComponent>(entity))
            return 0f;

        UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetAttackPower(in attack, in modifiers);
    }

    public static float GetAttackPower(in UnitAttackComponent attack, in UnitModifierComponent modifiers) =>
        modifiers.AttackPower.Apply(attack.BaseAttackPowerValue);

    public static float GetSkillRange(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitAttackComponent>(entity))
            return 0f;

        UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetSkillRange(in attack, in modifiers);
    }

    public static float GetSkillRange(in UnitAttackComponent attack, in UnitModifierComponent modifiers) =>
        modifiers.SkillRange.Apply(attack.BaseSkillRangeValue);

    public static float GetMaxMp(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitManaComponent>(entity))
            return 0f;

        UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetMaxMp(in mana, in modifiers);
    }

    public static float GetMaxMp(in UnitManaComponent mana, in UnitModifierComponent modifiers) =>
        modifiers.MaxMp.Apply(mana.BaseMaxMp + mana.BaseMaxMpOffset);

    public static float GetHealthRegen(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitVitalityComponent>(entity))
            return 0f;

        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetHealthRegen(in vitality, in modifiers);
    }

    public static float GetHealthRegen(in UnitVitalityComponent vitality, in UnitModifierComponent modifiers) =>
        modifiers.HealthRegen.Apply(vitality.BaseHealthRegenPerSecondValue);

    public static float GetMpRegen(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitManaComponent>(entity))
            return 0f;

        UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetMpRegen(in mana, in modifiers);
    }

    public static float GetMpRegen(in UnitManaComponent mana, in UnitModifierComponent modifiers) =>
        modifiers.MpRegen.Apply(mana.BaseMpRegenPerSecond + mana.BaseMpRegenPerSecondOffset);

    public static float GetChantSpeedBonus(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitAttackComponent>(entity))
            return 0f;

        UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetChantSpeedBonus(in attack, in modifiers);
    }

    public static float GetChantSpeedBonus(in UnitAttackComponent attack, in UnitModifierComponent modifiers) =>
        math.clamp(modifiers.ChantSpeed.Apply(attack.BaseChantSpeedBonusValue), -100f, 100f);

    public static float GetElementPower(EntityManager entityManager, Entity entity, ElementType elementType)
    {
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return 0f;

        UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(entity);
        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return GetElementPower(in element, in modifiers, elementType);
    }

    public static float GetElementPower(
        in UnitElementComponent element,
        in UnitModifierComponent modifiers,
        ElementType elementType)
    {
        PropertyModifierChannel channel = elementType switch
        {
            ElementType.Water => PropertyModifierChannel.WaterPower,
            ElementType.Fire => PropertyModifierChannel.FirePower,
            ElementType.Lightning => PropertyModifierChannel.LightningPower,
            ElementType.Wind => PropertyModifierChannel.WindPower,
            _ => default,
        };
        return elementType == ElementType.None
            ? 0f
            : modifiers.Get(channel).Apply(element.GetPowerBonus(elementType));
    }

    public static SkillModifierSet BuildPersistentSkillModifiers(EntityManager entityManager, Entity entity)
    {
        return entityManager.HasComponent<UnitModifierComponent>(entity)
            ? entityManager.GetComponentData<UnitModifierComponent>(entity).SkillModifiers
            : default;
    }

    public static float ApplyDamageTakenModifiers(EntityManager entityManager, Entity entity, float damage)
    {
        if (damage <= 0f)
            return 0f;

        UnitModifierComponent modifiers = GetModifiers(entityManager, entity);
        return ApplyDamageTakenModifiers(in modifiers, damage);
    }

    public static float ApplyDamageTakenModifiers(in UnitModifierComponent modifiers, float damage) =>
        damage <= 0f ? 0f : math.max(0f, modifiers.DamageTakenMultiplier.Apply(damage));

    public static bool TryCaptureElementState(EntityManager entityManager, Entity entity, out UnitElementComponent element)
    {
        element = default;
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return false;

        element = entityManager.GetComponentData<UnitElementComponent>(entity);
        element.WaterPower = GetElementPower(entityManager, entity, ElementType.Water);
        element.FirePower = GetElementPower(entityManager, entity, ElementType.Fire);
        element.LightningPower = GetElementPower(entityManager, entity, ElementType.Lightning);
        element.WindPower = GetElementPower(entityManager, entity, ElementType.Wind);
        return true;
    }

    private static UnitModifierComponent GetModifiers(EntityManager entityManager, Entity entity)
    {
        return entityManager.HasComponent<UnitModifierComponent>(entity)
            ? entityManager.GetComponentData<UnitModifierComponent>(entity)
            : UnitModifierComponent.CreateIdentity();
    }
}
