using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("Static", 0, "Static")]
    internal sealed class StaticSkillFollowupModifierRule : SkillFollowupModifierRule
    {
        public override bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, ref UnitCastFollowupEffectElement followup)
        {
            if (ruleData is not StaticSkillFollowupModifierRuleData staticRuleData)
                return false;

            followup.ModifierEntries = default;
            followup.ModifierSlices = default;
            followup.ModifierRuleStateInt0 = 0;
            followup.ModifierRuleStateFloat0 = 0f;

            return SkillFollowupModifierRuntimeUtility.TryAppendModifierSlice(ref followup, staticRuleData.Modifiers);
        }

        public override void GetModifier(ref SkillModifierSet modifiers, in UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Length <= 0)
                return;

            SkillFollowupModifierRuntimeUtility.ApplySliceModifiers(ref modifiers, followup.ModifierEntries, followup.ModifierSlices[0]);
        }

        public override void OnConsumed(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
        }
    }
}
