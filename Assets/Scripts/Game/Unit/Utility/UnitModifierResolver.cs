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
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.MoveSpeed).Apply(move.BaseMoveSpeedValue);
    }

    public static float GetMaxAcceleration(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitMoveComponent>(entity))
            return 0f;

        UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.MoveSpeed).Apply(move.BaseMaxAcceleration);
    }

    public static float GetMaxHealth(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitVitalityComponent>(entity))
            return 0f;

        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.MaxHealth).Apply(vitality.BaseMaxHealthValue);
    }

    public static float GetDefense(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitVitalityComponent>(entity))
            return 0f;

        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.Defense).Apply(vitality.BaseDefenseValue);
    }

    public static float GetAttackPower(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitAttackComponent>(entity))
            return 0f;

        UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.AttackPower).Apply(attack.BaseAttackPowerValue);
    }

    public static float GetSkillRange(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitAttackComponent>(entity))
            return 0f;

        UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.SkillRange).Apply(attack.BaseSkillRangeValue);
    }

    public static float GetMaxMp(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitManaComponent>(entity))
            return 0f;

        UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.MaxMp).Apply(mana.BaseMaxMp + mana.BaseMaxMpOffset);
    }

    public static float GetHealthRegen(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitVitalityComponent>(entity))
            return 0f;

        UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.HealthRegen).Apply(vitality.BaseHealthRegenPerSecondValue);
    }

    public static float GetMpRegen(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitManaComponent>(entity))
            return 0f;

        UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
        return GetPropertyModifier(entityManager, entity, PropertyModifierChannel.MpRegen).Apply(mana.BaseMpRegenPerSecond + mana.BaseMpRegenPerSecondOffset);
    }

    public static float GetChantSpeedBonus(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitAttackComponent>(entity))
            return 0f;

        UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(entity);
        float value = GetPropertyModifier(entityManager, entity, PropertyModifierChannel.ChantSpeed).Apply(attack.BaseChantSpeedBonusValue);
        return math.clamp(value, -100f, 100f);
    }

    public static float GetElementPower(EntityManager entityManager, Entity entity, ElementType elementType)
    {
        if (!entityManager.HasComponent<UnitElementComponent>(entity))
            return 0f;

        UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(entity);
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
            : GetPropertyModifier(entityManager, entity, channel).Apply(element.GetPowerBonus(elementType));
    }

    public static SkillModifierSet BuildPersistentSkillModifiers(EntityManager entityManager, Entity entity)
    {
        SkillModifierSet modifiers = new();
        if (!UnitBuffUtility.TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent) ||
            runtimeComponent.Buffs == null)
        {
            return modifiers;
        }

        for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
        {
            UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
            if (entry != null && entry.StackCount > 0)
                entry.ContributeSkillModifiers(modifiers);
        }

        return modifiers;
    }

    public static float ApplyDamageTakenModifiers(EntityManager entityManager, Entity entity, float damage)
    {
        if (damage <= 0f)
            return 0f;

        return math.max(0f, GetPropertyModifier(entityManager, entity, PropertyModifierChannel.DamageTakenMultiplier).Apply(damage));
    }

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

    private static ModifierValue GetPropertyModifier(EntityManager entityManager, Entity entity, PropertyModifierChannel channel)
    {
        PropertyModifierSet modifiers = new();
        if (UnitBuffUtility.TryGetRuntimeComponent(entityManager, entity, out UnitBuffRuntimeComponent runtimeComponent) &&
            runtimeComponent.Buffs != null)
        {
            for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
            {
                UnitBuffRuntimeEntry entry = runtimeComponent.Buffs[i];
                if (entry != null && entry.StackCount > 0)
                    entry.ContributePropertyModifiers(modifiers);
            }
        }

        return new ModifierValue(modifiers.GetFactor(channel), modifiers.GetBonus(channel));
    }

    private readonly struct ModifierValue
    {
        public ModifierValue(float factor, float bonus)
        {
            Factor = factor;
            Bonus = bonus;
        }

        public float Factor { get; }
        public float Bonus { get; }

        public float Apply(float baseValue) => baseValue * Factor + Bonus;
    }
}
