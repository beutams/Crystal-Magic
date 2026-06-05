using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class DamageEffectData : EffectData
    {
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
            AppendElementModifiers(GetAttributePowerValue(runtimeModifiers), runtimeModifiers);
            DamageEffectData copy = (DamageEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementComponent);
            copy.DamageCoefficient = ApplyModifier(runtimeModifiers, SkillModifierChannel.Damage, DamageCoefficient);
            copy.FlatDamageBonus = ApplyModifier(runtimeModifiers, SkillModifierChannel.FlatDamage, FlatDamageBonus);
            return copy;
        }

        private void AppendElementModifiers(float elementBonus, SkillModifierSet modifiers)
        {
            if (Element == ElementType.None)
                return;

            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.Damage,
                Factor = elementBonus,
            });
            modifiers.Add(new SkillModifierEntry
            {
                Channel = SkillModifierChannel.FlatDamage,
                Factor = elementBonus,
            });
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
