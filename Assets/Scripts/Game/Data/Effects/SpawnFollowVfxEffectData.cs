using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    public enum SpawnVfxFollowTarget : byte
    {
        OriginEntity = 0,
        TargetEntity = 1,
    }

    [System.Serializable]
    public sealed class SpawnFollowVfxEffectData : EffectData
    {
        [EditorLabel("特效预制体名称")]
        public string VfxPrefabName;

        [EditorLabel("跟随目标")]
        public SpawnVfxFollowTarget FollowTarget;

        [EditorLabel("循环时长（0 为持续）")]
        public float Duration;

        [EditorLabel("缩放")]
        public float Scale = 1f;

        [EditorLabel("生成偏移")]
        public Vector3 SpawnOffset;

        [EditorLabel("对齐目标朝向")]
        public bool AlignToTargetForward;

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            SpawnFollowVfxEffectData copy = (SpawnFollowVfxEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, Duration);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Scale);
            return copy;
        }
    }
}
