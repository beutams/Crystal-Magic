using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data
{
    public enum SkillFollowupModifierRuleType
    {
        Static = 0,
        Sequence = 1,
    }

    [Serializable]
    public abstract class SkillFollowupModifierRuleData
    {
        public abstract SkillFollowupModifierRuleType RuleType { get; }
    }

    [Serializable]
    public sealed class StaticSkillFollowupModifierRuleData : SkillFollowupModifierRuleData
    {
        [EditorLabel("Modifiers")]
        public List<SkillModifierEntry> Modifiers = new();

        public override SkillFollowupModifierRuleType RuleType => SkillFollowupModifierRuleType.Static;
    }

    [Serializable]
    public sealed class SequenceSkillFollowupModifierRuleData : SkillFollowupModifierRuleData
    {
        [EditorLabel("Modifier Sets")]
        public List<SkillFollowupModifierSetData> ModifierSets = new();

        public override SkillFollowupModifierRuleType RuleType => SkillFollowupModifierRuleType.Sequence;
    }

    [Serializable]
    public sealed class SkillFollowupModifierSetData
    {
        [EditorLabel("Modifiers")]
        public List<SkillModifierEntry> Modifiers = new();
    }
}
