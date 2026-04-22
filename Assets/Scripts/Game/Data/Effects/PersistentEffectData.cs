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

        /// <summary>开始时立即触发的效果链</summary>
        [UnityEngine.SerializeReference]
        public EffectData[] OnStartEffects = System.Array.Empty<EffectData>();

        /// <summary>每次周期触发时执行的效果链</summary>
        [UnityEngine.SerializeReference]
        public EffectData[] OnTickEffects = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            PersistentEffectData copy = (PersistentEffectData)base.CreateRuntimeCopy(modifiers);
            copy.TotalDuration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, TotalDuration);
            copy.TickIntervalSeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.TickInterval, TickIntervalSeconds);
            copy.OnStartEffects = CreateRuntimeCopies(OnStartEffects, modifiers);
            copy.OnTickEffects = CreateRuntimeCopies(OnTickEffects, modifiers);
            return copy;
        }
    }
}
