using CrystalMagic.Game.Data;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 生成音效效果的配置数据
    /// </summary>
    [System.Serializable]
    public sealed class SpawnSoundEffectData : EffectData
    {
        /// <summary>音频资源路径（相对 Resources/）</summary>
        [EditorLabel("音频路径")]
        public string AudioPath;

        [EditorLabel("音频通道")]
        public AudioChannel Channel = AudioChannel.Unit;

        /// <summary>音量，0–1</summary>
        [EditorLabel("音量")]
        public float Volume = 1f;

        /// <summary>音调，1 = 原始音调</summary>
        [EditorLabel("音调")]
        public float Pitch = 1f;

        /// <summary>空间混合，0 = 纯 2D，1 = 纯 3D</summary>
        [EditorLabel("空间混合")]
        public float SpatialBlend;

        /// <summary>播放延迟（秒）</summary>
        [EditorLabel("延迟秒数")]
        public float DelaySeconds;

        /// <summary>是否跟随施法者移动</summary>
        [EditorLabel("跟随施法者")]
        public bool FollowCaster;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            SpawnSoundEffectData copy = (SpawnSoundEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.Volume = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundVolume, Volume);
            copy.Pitch = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundPitch, Pitch);
            copy.DelaySeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundDelay, DelaySeconds);
            return copy;
        }
    }
}
