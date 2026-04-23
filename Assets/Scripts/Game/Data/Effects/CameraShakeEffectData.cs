using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class CameraShakeEffectData : EffectData
    {
        public float Duration = 0.15f;
        public float Amplitude = 0.15f;
        public float Frequency = 25f;
        public bool UseDistanceAttenuation;
        public float Radius;
        public Vector3 PositionOffset;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers)
        {
            CameraShakeEffectData copy = (CameraShakeEffectData)base.CreateRuntimeCopy(modifiers);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeDuration, Duration);
            copy.Amplitude = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeAmplitude, Amplitude);
            copy.Frequency = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeFrequency, Frequency);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeRadius, Radius);
            return copy;
        }
    }
}
