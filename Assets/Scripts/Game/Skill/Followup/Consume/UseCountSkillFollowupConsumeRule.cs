using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("UseCount", 0, "Use Count")]
    internal sealed class UseCountSkillFollowupConsumeRule : SkillFollowupConsumeRule
    {
        public override bool TryInitializeRuntime(SkillFollowupConsumeRuleData ruleData, ref UnitCastFollowupEffectElement followup)
        {
            if (ruleData is not UseCountSkillFollowupConsumeRuleData useCountRuleData)
                return false;

            followup.ConsumeRuleStateInt0 = Mathf.Max(1, useCountRuleData.Uses);
            followup.ConsumeRuleStateFloat0 = 0f;
            return true;
        }

        public override bool CanApply(in UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            return followup.ConsumeRuleStateInt0 > 0;
        }

        public override bool Consume(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            followup.ConsumeRuleStateInt0 -= 1;
            return followup.ConsumeRuleStateInt0 > 0;
        }
    }
}
