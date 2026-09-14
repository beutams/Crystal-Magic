namespace CrystalMagic.Game.Data.Effects
{
    public enum BuffTargetSource
    {
        [EditorLabel("当前目标")]
        CurrentTarget = 0,
        [EditorLabel("事件另一方")]
        OtherEntity = 1,
    }

    [System.Serializable]
    public sealed class ApplyBuffEffectData : EffectData
    {
        [EditorLabel("BuffId")]
        public int BuffId = -1;

        [EditorLabel("施加目标")]
        public BuffTargetSource TargetSource = BuffTargetSource.CurrentTarget;

        [EditorLabel("持续时间")]
        public float DurationSeconds = 1f;

        [EditorLabel("施加层数")]
        public int StackCount = 1;

        [EditorLabel("每个持续效果仅一次")]
        public bool OnlyOncePerPersistentEffect;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            ApplyBuffEffectData copy = (ApplyBuffEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.DurationSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.BuffDuration, DurationSeconds);
            return copy;
        }
    }
}
