using CrystalMagic.Game.Data;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class SpawnVfxEffectData : EffectData
    {
        [EditorLabel("特效贴图")]
        public Texture2D VfxTexture;
        [EditorLabel("帧数")]
        public int FrameCount = 16;
        [EditorLabel("循环")]
        public bool Loop;
        [EditorLabel("循环时长")]
        public float Duration;
        [EditorLabel("缩放")]
        public float Scale = 1f;
        [EditorLabel("生成偏移")]
        public Vector3 SpawnOffset;
        [EditorLabel("跟随施法者")]
        public bool FollowCaster;
        [EditorLabel("对齐施法者朝向")]
        public bool AlignToCasterForward;

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            SpawnVfxEffectData copy = (SpawnVfxEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, Duration);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Scale);
            copy.FrameCount = math.clamp(FrameCount, 1, 16);
            return copy;
        }
    }
}
