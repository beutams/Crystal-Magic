using CrystalMagic.Game.Data;
using Unity.Entities;

public struct UnitModifierComponent : IComponentData
{
    public PropertyModifierValue MoveSpeed;
    public PropertyModifierValue MaxHealth;
    public PropertyModifierValue Defense;
    public PropertyModifierValue AttackPower;
    public PropertyModifierValue SkillRange;
    public PropertyModifierValue MaxMp;
    public PropertyModifierValue HealthRegen;
    public PropertyModifierValue MpRegen;
    public PropertyModifierValue ChantSpeed;
    public PropertyModifierValue WaterPower;
    public PropertyModifierValue FirePower;
    public PropertyModifierValue LightningPower;
    public PropertyModifierValue WindPower;
    public PropertyModifierValue DamageTakenMultiplier;
    public SkillModifierSet SkillModifiers;

    public static UnitModifierComponent CreateIdentity()
    {
        PropertyModifierValue identity = PropertyModifierValue.Identity;
        return new UnitModifierComponent
        {
            MoveSpeed = identity,
            MaxHealth = identity,
            Defense = identity,
            AttackPower = identity,
            SkillRange = identity,
            MaxMp = identity,
            HealthRegen = identity,
            MpRegen = identity,
            ChantSpeed = identity,
            WaterPower = identity,
            FirePower = identity,
            LightningPower = identity,
            WindPower = identity,
            DamageTakenMultiplier = identity,
        };
    }

    public readonly PropertyModifierValue Get(PropertyModifierChannel channel)
    {
        return channel switch
        {
            PropertyModifierChannel.MoveSpeed => MoveSpeed,
            PropertyModifierChannel.MaxHealth => MaxHealth,
            PropertyModifierChannel.Defense => Defense,
            PropertyModifierChannel.AttackPower => AttackPower,
            PropertyModifierChannel.SkillRange => SkillRange,
            PropertyModifierChannel.MaxMp => MaxMp,
            PropertyModifierChannel.HealthRegen => HealthRegen,
            PropertyModifierChannel.MpRegen => MpRegen,
            PropertyModifierChannel.ChantSpeed => ChantSpeed,
            PropertyModifierChannel.WaterPower => WaterPower,
            PropertyModifierChannel.FirePower => FirePower,
            PropertyModifierChannel.LightningPower => LightningPower,
            PropertyModifierChannel.WindPower => WindPower,
            PropertyModifierChannel.DamageTakenMultiplier => DamageTakenMultiplier,
            _ => PropertyModifierValue.Identity,
        };
    }
}
