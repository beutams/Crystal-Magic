using CrystalMagic.Game.Data;
using Unity.Mathematics;

namespace CrystalMagic.Game.Data.Effects
{
    public enum DamageTargetSource
    {
        [EditorLabel("当前目标")]
        CurrentTarget = 0,
        [EditorLabel("事件另一方")]
        OtherEntity = 1,
    }

    public enum DamageValueSource
    {
        [EditorLabel("施法者攻击力")]
        AttackPower = 0,
        [EditorLabel("事件数值")]
        TriggerValue = 1,
    }

    [System.Serializable]
    public sealed class DamageEffectData : EffectData
    {
        [EditorLabel("伤害目标")]
        public DamageTargetSource TargetSource = DamageTargetSource.CurrentTarget;

        [EditorLabel("伤害基数")]
        public DamageValueSource ValueSource = DamageValueSource.AttackPower;

        [EditorLabel("伤害倍率")]
        public float DamageCoefficient;

        [EditorLabel("额外伤害")]
        public float FlatDamageBonus;

        [EditorLabel("元素类型")]
        public ElementType Element = ElementType.None;

        public override ElementType GetAttributeElementType()
        {
            return Element;
        }

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            float attributePower = ResolveAttributePower(elementComponent);
            SkillModifierSet runtimeModifiers = CreateModifiersWithAttributePower(modifiers, attributePower);
            DamageEffectData copy = (DamageEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementComponent);
            float skillDamageFactor = runtimeModifiers?.GetFactor(SkillModifierChannel.Damage) ?? 1f;
            float skillDamageBonus = runtimeModifiers?.GetBonus(SkillModifierChannel.Damage) ?? 0f;
            float elementMultiplier = Element == ElementType.None
                ? 1f
                : math.max(0f, 1f + GetAttributePowerValue(runtimeModifiers));
            copy.DamageCoefficient = DamageCoefficient * skillDamageFactor * elementMultiplier + skillDamageBonus;
            copy.FlatDamageBonus = ApplyModifier(runtimeModifiers, SkillModifierChannel.FlatDamage, FlatDamageBonus);
            return copy;
        }
    }

    [System.Serializable]
    public sealed class KnockbackEffectData : EffectData
    {
        [EditorLabel("击退力度")]
        public float Force;

        [EditorLabel("控制时长")]
        public float DurationSeconds = 0.2f;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            KnockbackEffectData copy = (KnockbackEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Force = ApplyModifierNonNegative(modifiers, SkillModifierChannel.KnockbackForce, Force);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.HitStunSeconds, DurationSeconds);
            return copy;
        }
    }
}
