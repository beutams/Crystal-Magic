using System;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("RuntimeType", 20, "Runtime Type")]
    public sealed class RuntimeTypeFollowupFilter : SkillFollowupFilter
    {
        public override bool TryInitializeRuntime(SkillFollowupFilterData filterData, SkillFollowupRuntimeState followup)
        {
            if (filterData is not RuntimeTypeFollowupFilterData typedData)
                return false;

            followup.RuntimeType = typedData.EffectiveRuntimeType ?? string.Empty;
            return true;
        }

        public override bool IsMatch(SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
            return context.SkillData != null &&
                   string.Equals(context.SkillData.EffectiveRuntimeType, followup.RuntimeType, StringComparison.Ordinal);
        }
    }
}
