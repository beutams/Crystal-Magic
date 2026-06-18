using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class ForwardRectSearchEffectData : EffectData
    {
        [EditorLabel("Length")]
        public float Length = 1f;

        [EditorLabel("Width")]
        public float Width = 1f;

        [EditorLabel("Origin Offset")]
        public float OriginOffsetDistance;

        [EditorLabel("Target Conditions")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("On After Search")]
        public EffectData[] OnAfterSearch = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            ForwardRectSearchEffectData copy = (ForwardRectSearchEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Length = Length < 0f ? 0f : Length;
            copy.Width = Width < 0f ? 0f : Width;
            copy.OriginOffsetDistance = OriginOffsetDistance < 0f ? 0f : OriginOffsetDistance;
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementComponent);
            return copy;
        }
    }
}
