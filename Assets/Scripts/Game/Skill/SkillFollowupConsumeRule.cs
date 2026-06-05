using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public abstract class SkillFollowupConsumeRule
    {
        public abstract bool TryInitializeRuntime(SkillFollowupConsumeRuleData ruleData, SkillFollowupRuntimeState followup);

        public abstract bool CanApply(SkillFollowupRuntimeState followup, in SkillFollowupContext context);

        public abstract bool Consume(SkillFollowupRuntimeState followup, in SkillFollowupContext context);
    }
}
