using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Data
{
    public enum SkillFollowupFilterType
    {
        AnySkill = 0,
        SkillId = 1,
        SkillType = 2,
        Element = 3,
        SkillAdditionId = 4,
    }

    [Serializable]
    public class SkillFollowupEffectData
    {
        [EditorLabel("Filter")]
        public SkillFollowupFilterType FilterType;

        // Keep the old field name so existing JSON can still flow into the new default rule once.
        public int Uses = 1;

        [EditorLabel("Consume Rule")]
        public SkillFollowupConsumeRuleData ConsumeRule = new UseCountSkillFollowupConsumeRuleData();

        [EditorLabel("Modifier Rule")]
        public SkillFollowupModifierRuleData ModifierRule = new StaticSkillFollowupModifierRuleData();

        [EditorLabel("Skill Id")]
        public int SkillId = -1;

        [EditorLabel("Skill Type")]
        public SkillType SkillType;

        [EditorLabel("Element")]
        public ElementType Element = ElementType.None;

        [EditorLabel("Skill Addition Id")]
        public int SkillAdditionId = -1;

        // Keep the old field name so existing JSON can still flow into the new default modifier rule once.
        [EditorLabel("Modifiers")]
        public List<SkillModifierEntry> Modifiers = new();

        public void EnsureDefaults()
        {
            if (ConsumeRule == null)
                ConsumeRule = new UseCountSkillFollowupConsumeRuleData { Uses = Math.Max(1, Uses) };

            Modifiers ??= new List<SkillModifierEntry>();

            if (ModifierRule == null)
                ModifierRule = new StaticSkillFollowupModifierRuleData { Modifiers = new List<SkillModifierEntry>(Modifiers) };

            switch (ModifierRule)
            {
                case StaticSkillFollowupModifierRuleData staticRuleData:
                    staticRuleData.Modifiers ??= new List<SkillModifierEntry>();
                    break;
                case SequenceSkillFollowupModifierRuleData sequenceRuleData:
                    sequenceRuleData.ModifierSets ??= new List<SkillFollowupModifierSetData>();
                    for (int i = 0; i < sequenceRuleData.ModifierSets.Count; i++)
                    {
                        sequenceRuleData.ModifierSets[i] ??= new SkillFollowupModifierSetData();
                        sequenceRuleData.ModifierSets[i].Modifiers ??= new List<SkillModifierEntry>();
                    }

                    break;
            }
        }
    }
}
