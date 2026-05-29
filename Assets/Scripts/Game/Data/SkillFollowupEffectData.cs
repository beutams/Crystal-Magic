using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Data
{
    public enum SkillFollowupFilterType
    {
        AnySkill = 0,
        SkillId = 1,
        RuntimeType = 2,
        Element = 3,
        SkillAdditionId = 4,
    }

    [Serializable]
    public class SkillFollowupEffectData
    {
        [EditorLabel("Filter")]
        public SkillFollowupFilterType FilterType;

        [EditorLabel("Consume Rule")]
        public SkillFollowupConsumeRuleData ConsumeRule = new UseCountSkillFollowupConsumeRuleData();

        [EditorLabel("Modifier Rule")]
        public SkillFollowupModifierRuleData ModifierRule = new StaticSkillFollowupModifierRuleData();

        [EditorLabel("Skill Id")]
        public int SkillId = -1;

        [EditorLabel("Runtime Type")]
        public string RuntimeType;

        [EditorLabel("Element")]
        public ElementType Element = ElementType.None;

        [EditorLabel("Skill Addition Id")]
        public int SkillAdditionId = -1;

        public string EffectiveRuntimeType => SkillData.GetEffectiveRuntimeType(RuntimeType);

        public void EnsureDefaults()
        {
            if (ConsumeRule == null)
                ConsumeRule = new UseCountSkillFollowupConsumeRuleData();
            ConsumeRule.EnsureDefaults();

            if (ModifierRule == null)
                ModifierRule = new StaticSkillFollowupModifierRuleData();
            ModifierRule.EnsureDefaults();
        }
    }
}
