using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("SkillId", 10, "Skill Id")]
    public sealed class SkillIdFollowupFilter : SkillFollowupFilter
    {
        public override bool TryInitializeRuntime(SkillFollowupFilterData filterData, SkillFollowupRuntimeState followup)
        {
            if (filterData is not SkillIdFollowupFilterData typedData)
                return false;

            followup.SkillId = typedData.SkillId;
            return true;
        }

        public override bool IsMatch(SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
            return context.SkillData != null && followup.SkillId >= 0 && context.SkillData.Id == followup.SkillId;
        }
    }
}
