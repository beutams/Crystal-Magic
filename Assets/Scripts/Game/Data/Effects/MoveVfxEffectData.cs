using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class MoveVfxEffectData : EffectData
    {
        [EditorLabel("VFX prefab name")]
        public string VfxPrefabName;

        [EditorLabel("Move duration")]
        public float Duration = 0.5f;

        [EditorLabel("Scale")]
        public float Scale = 1f;

        [EditorLabel("Start offset")]
        public Vector3 StartOffset;

        [EditorLabel("Move offset")]
        public Vector3 MoveOffset;

        [EditorLabel("Preserve prefab rotation")]
        public bool PreservePrefabRotation = true;

        [EditorLabel("On arrival effects")]
        [SerializeReference]
        public EffectData[] OnArrivalEffects = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            MoveVfxEffectData copy = (MoveVfxEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.EffectDuration, Duration);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, Scale);
            copy.OnArrivalEffects = CreateRuntimeCopies(OnArrivalEffects, modifiers, elementComponent);
            return copy;
        }
    }
}
