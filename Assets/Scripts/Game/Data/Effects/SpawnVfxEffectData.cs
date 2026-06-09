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

        [EditorLabel("列数")]
        public int GridColumns = 4;

        [EditorLabel("行数")]
        public int GridRows = 4;

        [EditorLabel("总帧数")]
        public int FrameCount = 16;

        [EditorLabel("每秒帧数")]
        public float FramesPerSecond = 16f;

        [EditorLabel("循环")]
        public bool Loop;

        [EditorLabel("循环时长")]
        public float Duration;

        [EditorLabel("缩放")]
        public float Scale = 1f;

        [EditorLabel("宽度")]
        public float Width = 1f;

        [EditorLabel("高度")]
        public float Height = 1f;

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
            copy.Width = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Width > 0f ? Width : Scale);
            copy.Height = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Height > 0f ? Height : Scale);
            copy.GridColumns = math.max(1, GridColumns);
            copy.GridRows = math.max(1, GridRows);
            copy.FrameCount = math.clamp(FrameCount, 1, copy.GridColumns * copy.GridRows);
            copy.FramesPerSecond = math.max(0.01f, FramesPerSecond);
            return copy;
        }
    }
}
