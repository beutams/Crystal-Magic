namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class ApplyBuffEffectData : EffectData
    {
        [EditorLabel("BuffId")]
        public int BuffId = -1;

        [EditorLabel("持续时间")]
        public float DurationSeconds = 1f;

        [EditorLabel("施加层数")]
        public int StackCount = 1;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            ApplyBuffEffectData copy = (ApplyBuffEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.BuffDuration, DurationSeconds);
            return copy;
        }
    }
}
