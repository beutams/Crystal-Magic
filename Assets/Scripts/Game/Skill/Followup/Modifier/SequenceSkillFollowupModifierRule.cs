using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("Sequence", 10, "Sequence")]
    internal sealed class SequenceSkillFollowupModifierRule : SkillFollowupModifierRule
    {
        public override bool TryInitializeRuntime(SkillFollowupModifierRuleData ruleData, ref UnitCastFollowupEffectElement followup)
        {
            if (ruleData is not SequenceSkillFollowupModifierRuleData sequenceRuleData)
                return false;

            followup.ModifierEntries = default;
            followup.ModifierSlices = default;
            followup.ModifierRuleStateInt0 = 0;
            followup.ModifierRuleStateFloat0 = 0f;

            if (sequenceRuleData.ModifierSets == null || sequenceRuleData.ModifierSets.Count <= 0)
                return false;

            bool hasSlice = false;
            for (int i = 0; i < sequenceRuleData.ModifierSets.Count; i++)
            {
                SkillFollowupModifierSetData modifierSet = sequenceRuleData.ModifierSets[i];
                if (modifierSet?.Modifiers == null || modifierSet.Modifiers.Count <= 0)
                    continue;

                if (!SkillFollowupModifierRuntimeUtility.TryAppendModifierSlice(ref followup, modifierSet.Modifiers))
                    return false;

                hasSlice = true;
            }

            return hasSlice;
        }

        public override void GetModifier(ref SkillModifierSet modifiers, in UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Length <= 0)
                return;

            int sliceIndex = Mathf.Clamp(followup.ModifierRuleStateInt0, 0, followup.ModifierSlices.Length - 1);
            SkillFollowupModifierRuntimeUtility.ApplySliceModifiers(ref modifiers, followup.ModifierEntries, followup.ModifierSlices[sliceIndex]);
        }

        public override void OnConsumed(ref UnitCastFollowupEffectElement followup, in SkillFollowupContext context)
        {
            if (followup.ModifierSlices.Length <= 1)
                return;

            if (followup.ModifierRuleStateInt0 < followup.ModifierSlices.Length - 1)
                followup.ModifierRuleStateInt0 += 1;
        }
    }
}
