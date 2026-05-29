using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public abstract class SkillFollowupModifierRule
    {
        public abstract bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, ref UnitCastFollowupEffectElement followup);

        public abstract void GetModifier(ref SkillModifierSet modifiers, in UnitCastFollowupEffectElement followup, in SkillFollowupContext context);

        public abstract void OnConsumed(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context);
    }
}
