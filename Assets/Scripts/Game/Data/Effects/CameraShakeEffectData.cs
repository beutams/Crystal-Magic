using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class CameraShakeEffectData : EffectData
    {
        [EditorLabel("持续时间")]
        public float Duration = 0.15f;
        [EditorLabel("振幅")]
        public float Amplitude = 0.15f;
        [EditorLabel("频率")]
        public float Frequency = 25f;
        [EditorLabel("按距离衰减")]
        public bool UseDistanceAttenuation;
        [EditorLabel("作用半径")]
        public float Radius;
        [EditorLabel("位置偏移")]
        public Vector3 PositionOffset;

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, float elementBonus = 0f, System.Func<EffectData, float> elementBonusResolver = null)
        {
            CameraShakeEffectData copy = (CameraShakeEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.Duration = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeDuration, Duration);
            copy.Amplitude = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeAmplitude, Amplitude);
            copy.Frequency = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeFrequency, Frequency);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.CameraShakeRadius, Radius);
            return copy;
        }
    }
}
