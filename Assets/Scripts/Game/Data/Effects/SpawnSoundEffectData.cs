using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 生成音效效果的配置数据。
    /// </summary>
    [System.Serializable]
    public sealed class SpawnSoundEffectData : EffectData
    {
        /// <summary>
        /// 音频文件名，运行时会按通道拼到统一音频目录。
        /// </summary>
        [EditorLabel("音频文件名")]
        public string AudioPath;

        [EditorLabel("音频通道")]
        public AudioChannel Channel = AudioChannel.Unit;

        [EditorLabel("音量")]
        public float Volume = 1f;

        [EditorLabel("音调")]
        public float Pitch = 1f;

        [EditorLabel("空间混合")]
        public float SpatialBlend;

        [EditorLabel("延迟秒数")]
        public float DelaySeconds;

        [EditorLabel("跟随施法者")]
        public bool FollowCaster;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            SpawnSoundEffectData copy = (SpawnSoundEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Volume = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundVolume, Volume);
            copy.Pitch = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundPitch, Pitch);
            copy.DelaySeconds = ApplyModifierNonNegative(modifiers, SkillModifierChannel.SoundDelay, DelaySeconds);
            return copy;
        }
    }
}
