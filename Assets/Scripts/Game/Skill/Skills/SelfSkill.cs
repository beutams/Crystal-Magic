using CrystalMagic.Game.Data;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey(nameof(SelfSkill), 10, "Self Skill")]
    public sealed class SelfSkill : Skill
    {
        public SelfSkill(ResolvedSkillData data) : base(data)
        {
        }

        protected override bool BuildContext(in SkillReleaseRequest request, SkillContent context)
        {
            SetPosition(context, true, request.OriginPosition);
            SetTargetEntity(context, true, request.OriginEntity);
            return true;
        }
    }
}
