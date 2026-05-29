using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public abstract class SkillFollowupConsumeRule
    {
        public abstract bool TryInitializeRuntime(SkillFollowupConsumeRuleData ruleData, ref UnitCastFollowupEffectElement followup);

        public abstract bool CanApply(in UnitCastFollowupEffectElement followup, in SkillFollowupContext context);

        public abstract bool Consume(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context);
    }
}
