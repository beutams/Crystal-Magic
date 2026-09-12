using CrystalMagic.Game.Data;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class SpawnLineVfxEffectData : EffectData
    {
        [EditorLabel("VFX prefab name")]
        public string VfxPrefabName;

        [EditorLabel("Line length")]
        public float Length = 1f;

        [EditorLabel("Segment spacing")]
        public float SegmentSpacing = 1f;

        [EditorLabel("Origin offset")]
        public float OriginOffsetDistance;

        [EditorLabel("Duration")]
        public float Duration = 0.5f;

        [EditorLabel("Scale")]
        public float Scale = 1f;

        [EditorLabel("Align to line direction")]
        public bool AlignToLineDirection = true;

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            SpawnLineVfxEffectData copy = (SpawnLineVfxEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Length = math.max(0f, Length);
            copy.SegmentSpacing = math.max(0.01f, SegmentSpacing);
            copy.OriginOffsetDistance = math.max(0f, OriginOffsetDistance);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, Duration);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Scale);
            return copy;
        }
    }
}
