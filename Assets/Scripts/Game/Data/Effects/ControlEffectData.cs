namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class StunEffectData : EffectData
    {
        [EditorLabel("控制时长")]
        public float DurationSeconds = 0.5f;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            StunEffectData copy = (StunEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.HitStunSeconds, DurationSeconds);
            return copy;
        }
    }

    [System.Serializable]
    public sealed class FearEffectData : EffectData
    {
        [EditorLabel("控制时长")]
        public float DurationSeconds = 1f;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            FearEffectData copy = (FearEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.HitStunSeconds, DurationSeconds);
            return copy;
        }
    }
}
