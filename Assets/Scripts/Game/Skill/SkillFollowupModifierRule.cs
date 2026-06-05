using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public abstract class SkillFollowupModifierRule
    {
        public abstract bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, SkillFollowupRuntimeState followup);

        public abstract void GetModifier(ref SkillModifierSet modifiers, SkillFollowupRuntimeState followup, in SkillFollowupContext context);

        public abstract void OnConsumed(SkillFollowupRuntimeState followup, in SkillFollowupContext context);
    }
}
