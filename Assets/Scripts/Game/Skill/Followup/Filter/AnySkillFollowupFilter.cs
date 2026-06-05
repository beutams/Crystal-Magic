using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("AnySkill", 0, "Any Skill")]
    public sealed class AnySkillFollowupFilter : SkillFollowupFilter
    {
        public override bool TryInitializeRuntime(SkillFollowupFilterData filterData, SkillFollowupRuntimeState followup)
        {
            return filterData is AnySkillFollowupFilterData;
        }

        public override bool IsMatch(SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
            return true;
        }
    }
}
