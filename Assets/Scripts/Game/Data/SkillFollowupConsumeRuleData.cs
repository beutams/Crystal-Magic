using System;

namespace CrystalMagic.Game.Data
{
    public enum SkillFollowupConsumeRuleType
    {
        UseCount = 0,
    }

    [Serializable]
    public abstract class SkillFollowupConsumeRuleData
    {
        public abstract SkillFollowupConsumeRuleType RuleType { get; }
    }

    [Serializable]
    public sealed class UseCountSkillFollowupConsumeRuleData : SkillFollowupConsumeRuleData
    {
        [EditorLabel("Use Count")]
        public int Uses = 1;

        public override SkillFollowupConsumeRuleType RuleType => SkillFollowupConsumeRuleType.UseCount;
    }
}
