using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("Static", 0, "Static")]
    internal sealed class StaticSkillFollowupModifierRule : SkillFollowupModifierRule
    {
        public override bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, SkillFollowupRuntimeState followup)
        {
            if (ruleData is not StaticSkillFollowupModifierRuleData staticRuleData)
                return false;

            followup.ModifierEntries.Clear();
            followup.ModifierSlices.Clear();
            followup.ModifierRuleStateInt0 = 0;
            followup.ModifierRuleStateFloat0 = 0f;

            return SkillFollowupModifierRuntimeUtility.TryAppendModifierSlice(followup, staticRuleData.Modifiers);
        }

        public override void GetModifier(ref SkillModifierSet modifiers, SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Count <= 0)
                return;

            SkillFollowupModifierRuntimeUtility.ApplySliceModifiers(ref modifiers, followup.ModifierEntries, followup.ModifierSlices[0]);
        }

        public override void OnConsumed(SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
        }
    }
}
