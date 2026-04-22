using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 持续性效果（Buff / 场地效果）的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class PersistentEffectData : EffectData
    {
        /// <summary>总持续时间（秒）</summary>
        public float TotalDuration;

        /// <summary>周期性触发间隔（秒），0 = 不按 Tick 重复</summary>
        public float TickIntervalSeconds;

        /// <summary>开始时触发的 Payload 配置 Id</summary>
        public int OnStartPayloadId;

        /// <summary>每次 Tick 触发的 Payload 配置 Id</summary>
        public int TickPayloadId;

        /// <summary>结束时触发的 Payload 配置 Id</summary>
        public int OnEndPayloadId;

        /// <summary>影响半径（世界单位），0 = 仅施法者自身</summary>
        public float AffectRadius;

        /// <summary>是否以 SkillContent.Position 为中心（需上下文带位置）</summary>
        public bool AnchorToContextPosition;

        /// <summary>是否可叠层</summary>
        public bool CanStack;

        /// <summary>最大叠层数</summary>
        public int MaxStacks = 1;

        /// <summary>重新施加时是否重置为满持续时间</summary>
        public bool RefreshDurationOnReapply;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            PersistentEffectData copy = (PersistentEffectData)base.CreateRuntimeCopy(modifiers);
            copy.TotalDuration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, TotalDuration);
            copy.TickIntervalSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.TickInterval, TickIntervalSeconds);
            copy.AffectRadius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, AffectRadius);
            return copy;
        }
    }
}
