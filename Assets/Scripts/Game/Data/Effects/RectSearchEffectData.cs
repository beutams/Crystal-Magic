using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class RectSearchEffectData : EffectData
    {
        [EditorLabel("Center Offset")]
        public Vector3 CenterOffset;

        [EditorLabel("Size")]
        public Vector2 Size = Vector2.one;

        [EditorLabel("Use Horizontal Facing")]
        public bool UseHorizontalFacing;

        [EditorLabel("Target Conditions")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("On After Search")]
        public EffectData[] OnAfterSearch = System.Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(SkillModifierSet modifiers, UnitElementComponent? elementComponent = null)
        {
            RectSearchEffectData copy = (RectSearchEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Size = new Vector2(Mathf.Max(0f, Size.x), Mathf.Max(0f, Size.y));
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementComponent);
            return copy;
        }
    }
}
