using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class RandomAreaPointEffectData : EffectData
    {
        [EditorLabel("Radius")]
        public float Radius = 1f;

        [EditorLabel("Min Radius")]
        public float MinRadius;

        [EditorLabel("Point Count")]
        public int PointCount = 1;

        [EditorLabel("Center Offset")]
        public Vector3 CenterOffset;

        [EditorLabel("On Each Point Effects")]
        [SerializeReference]
        public EffectData[] OnEachPointEffects = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            RandomAreaPointEffectData copy = (RandomAreaPointEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, Radius);
            copy.MinRadius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, MinRadius);
            copy.PointCount = Unity.Mathematics.math.max(1, PointCount);
            copy.OnEachPointEffects = CreateRuntimeCopies(OnEachPointEffects, modifiers, elementComponent);
            return copy;
        }
    }
}
