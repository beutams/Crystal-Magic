using UnityEngine;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 生成特效效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class SpawnVfxEffectData : EffectData
    {
        /// <summary>特效预制体资源路径（相对 Resources/）</summary>
        public string VfxPath;

        /// <summary>持续时间（秒），0 = 由特效自身控制</summary>
        public float Duration;

        /// <summary>缩放倍率，1 = 原始大小</summary>
        public float Scale = 1f;

        /// <summary>相对施法者的生成偏移</summary>
        public Vector3 SpawnOffset;

        /// <summary>是否跟随施法者移动</summary>
        public bool FollowCaster;

        /// <summary>是否对齐施法者朝向</summary>
        public bool AlignToCasterForward;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            SpawnVfxEffectData copy = (SpawnVfxEffectData)base.CreateRuntimeCopy(modifiers);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, Duration);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Scale);
            return copy;
        }
    }
}
