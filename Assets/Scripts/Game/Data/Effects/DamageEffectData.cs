using CrystalMagic.Game.Data;
using Newtonsoft.Json;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class DamageEffectData : EffectData
    {
        public float DamageCoefficient;
        public float FlatDamageBonus;
        public ElementType Element = ElementType.None;

        [JsonProperty("DamageTypeId")]
        private ElementType LegacyDamageTypeId
        {
            set => Element = value;
        }

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            SkillModifierSet runtimeModifiers = CreateCombinedModifiers(modifiers, elementBonus, AppendElementModifiers);
            DamageEffectData copy = (DamageEffectData)base.CreateRuntimeCopy(runtimeModifiers, elementBonus, elementBonusResolver);
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
        public float Force;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            KnockbackEffectData copy = (KnockbackEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.Force = ApplyModifierNonNegative(modifiers, SkillModifierChannel.KnockbackForce, Force);
            return copy;
        }
    }

}
