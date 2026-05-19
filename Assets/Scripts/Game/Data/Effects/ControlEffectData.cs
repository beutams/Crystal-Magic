namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class StunEffectData : EffectData
    {
        [EditorLabel("控制时长")]
        public float DurationSeconds = 0.5f;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            StunEffectData copy = (StunEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.HitStunSeconds, DurationSeconds);
            return copy;
        }
    }

    [System.Serializable]
    public sealed class FearEffectData : EffectData
    {
        [EditorLabel("控制时长")]
        public float DurationSeconds = 1f;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            FearEffectData copy = (FearEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.HitStunSeconds, DurationSeconds);
            return copy;
        }
    }
}
