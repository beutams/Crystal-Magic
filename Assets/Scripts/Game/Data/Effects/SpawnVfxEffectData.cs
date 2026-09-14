using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class SpawnVfxEffectData : EffectData
    {
        [EditorLabel("特效预制体名称")]
        public string VfxPrefabName;

        [EditorLabel("循环时长（0 为持续）")]
        public float Duration;

        [EditorLabel("缩放")]
        public float Scale = 1f;

        [EditorLabel("生成偏移")]
        public Vector3 SpawnOffset;

        [EditorLabel("对齐施法者朝向")]
        public bool AlignToCasterForward;

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            SpawnVfxEffectData copy = (SpawnVfxEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, Duration);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Scale);
            return copy;
        }
    }
}
