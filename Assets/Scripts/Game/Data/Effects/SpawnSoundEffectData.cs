using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 生成音效效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class SpawnSoundEffectData : EffectData
    {
        /// <summary>音频资源路径（相对 Resources/）</summary>
        public string AudioPath;

        /// <summary>音量，0–1</summary>
        public float Volume = 1f;

        /// <summary>音调，1 = 原始音调</summary>
        public float Pitch = 1f;

        /// <summary>空间混合，0 = 纯 2D，1 = 纯 3D</summary>
        public float SpatialBlend;

        /// <summary>播放延迟（秒）</summary>
        public float DelaySeconds;

        /// <summary>是否跟随施法者移动</summary>
        public bool FollowCaster;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            SpawnSoundEffectData copy = (SpawnSoundEffectData)base.CreateRuntimeCopy(modifiers);
            copy.Volume = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundVolume, Volume);
            copy.Pitch = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundPitch, Pitch);
            copy.DelaySeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundDelay, DelaySeconds);
            return copy;
        }
    }
}
