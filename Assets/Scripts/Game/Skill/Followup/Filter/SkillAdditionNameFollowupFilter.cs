using System;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("SkillAdditionName", 40, "Skill Addition Name")]
    public sealed class SkillAdditionNameFollowupFilter : SkillFollowupFilter
    {
        public override bool TryInitializeRuntime(SkillFollowupFilterData filterData, SkillFollowupRuntimeState followup)
        {
            if (filterData is not SkillAdditionNameFollowupFilterData typedData)
                return false;

            followup.SkillAdditionName = typedData.SkillAdditionName ?? string.Empty;
            return true;
        }

        public override bool IsMatch(SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
            return context.SkillAdditionData != null &&
                   !string.IsNullOrWhiteSpace(followup.SkillAdditionName) &&
                   string.Equals(context.SkillAdditionData.NameKey, followup.SkillAdditionName, StringComparison.Ordinal);
        }
    }
}
