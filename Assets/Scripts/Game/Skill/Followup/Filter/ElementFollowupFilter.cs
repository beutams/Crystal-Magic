using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey("Element", 30, "Element")]
    public sealed class ElementFollowupFilter : SkillFollowupFilter
    {
        public override bool TryInitializeRuntime(SkillFollowupFilterData filterData, SkillFollowupRuntimeState followup)
        {
            if (filterData is not ElementFollowupFilterData typedData)
                return false;

            followup.Element = typedData.Element;
            return true;
        }

        public override bool IsMatch(SkillFollowupRuntimeState followup, in SkillFollowupContext context)
        {
            return context.SkillData != null &&
                   followup.Element != ElementType.None &&
                   SkillUsesElement(context.SkillData.EffectChain, followup.Element);
        }
    }
}
