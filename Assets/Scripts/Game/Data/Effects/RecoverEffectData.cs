using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class HealEffectData : EffectData
    {
        [EditorLabel("治疗倍率")]
        public float HealCoefficient;

        [EditorLabel("额外治疗")]
        public float FlatHealBonus;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            HealEffectData copy = (HealEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.HealCoefficient = ApplyModifier(modifiers, SkillModifierChannel.Heal, HealCoefficient);
            copy.FlatHealBonus = ApplyModifier(modifiers, SkillModifierChannel.FlatHeal, FlatHealBonus);
            return copy;
        }
    }

    [System.Serializable]
    public sealed class RestoreManaEffectData : EffectData
    {
        [EditorLabel("回蓝倍率")]
        public float ManaRestoreCoefficient;

        [EditorLabel("额外回蓝")]
        public float FlatManaRestoreBonus;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            RestoreManaEffectData copy = (RestoreManaEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.ManaRestoreCoefficient = ApplyModifier(modifiers, SkillModifierChannel.ManaRestore, ManaRestoreCoefficient);
            copy.FlatManaRestoreBonus = ApplyModifier(modifiers, SkillModifierChannel.FlatManaRestore, FlatManaRestoreBonus);
            return copy;
        }
    }
}
