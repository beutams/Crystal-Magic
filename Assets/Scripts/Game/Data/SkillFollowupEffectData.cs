using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public class SkillFollowupEffectData
    {
        [EditorLabel("Filter")]
        public SkillFollowupFilterData Filter = new AnySkillFollowupFilterData();

        [EditorLabel("Consume Rule")]
        public SkillFollowupConsumeRuleData ConsumeRule = new UseCountSkillFollowupConsumeRuleData();

        [EditorLabel("Modifier Rule")]
        public SkillFollowupModifierRuleData ModifierRule = new StaticSkillFollowupModifierRuleData();

        public void EnsureDefaults()
        {
            if (Filter == null)
                Filter = new AnySkillFollowupFilterData();
            Filter.EnsureDefaults();

            if (ConsumeRule == null)
                ConsumeRule = new UseCountSkillFollowupConsumeRuleData();
            ConsumeRule.EnsureDefaults();

            if (ModifierRule == null)
                ModifierRule = new StaticSkillFollowupModifierRuleData();
            ModifierRule.EnsureDefaults();
        }
    }
}
