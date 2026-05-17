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
            float elementBonus = 0f,
            Func<EffectData, float> elementBonusResolver = null)
        {
            ChainSearchEffectData copy = (ChainSearchEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.Radius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, Radius);
            copy.MaxJumps = MaxJumps < 0 ? 0 : MaxJumps;
            copy.TargetConditions = TargetConditions == null ? new List<ConditionConfig>() : new List<ConditionConfig>(TargetConditions);
            copy.OnAfterSearch = CreateRuntimeCopies(OnAfterSearch, modifiers, elementBonusResolver);
            return copy;
        }
    }
}
