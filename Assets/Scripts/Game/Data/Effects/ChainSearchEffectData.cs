using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data.Effects
{
    [Serializable]
    public sealed class ChainSearchEffectData : EffectData
    {
        [EditorLabel("Search Radius")]
        public float Radius = 1f;

        [EditorLabel("Max Jumps")]
        public int MaxJumps = 1;

        [EditorLabel("Target Conditions")]
        public List<ConditionConfig> TargetConditions = new();

        [EditorLabel("On After Search")]
        public EffectData[] OnAfterSearch = Array.Empty<EffectData>();

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            ChainSearchEffectData copy = (ChainSearchEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, Radius);
            copy.MaxJumps = MaxJumps < 0 ? 0 : MaxJumps;
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementComponent);
            return copy;
        }
    }
}
