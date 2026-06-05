using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class ConeSearchEffectData : EffectData
    {
        [EditorLabel("Radius")]
        public float Radius = 1f;

        [EditorLabel("Angle")]
        public float AngleDegrees = 90f;

        [EditorLabel("Target Conditions")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("On After Search")]
        public EffectData[] OnAfterSearch = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            ConeSearchEffectData copy = (ConeSearchEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, Radius);
            copy.AngleDegrees = Mathf.Clamp(AngleDegrees, 0f, 360f);
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementComponent);
            return copy;
        }
    }
}
