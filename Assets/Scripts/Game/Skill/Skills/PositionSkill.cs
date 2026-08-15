using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey(nameof(PositionSkill), 0, "Position Skill")]
    public sealed class PositionSkill : Skill
    {
        public PositionSkill(ResolvedSkillData data) : base(data)
        {
        }

        protected override bool BuildContext(in SkillReleaseRequest request, SkillContent context)
        {
            SetPosition(
                context,
                request.HasTargetPosition,
                new Vector3(request.TargetPosition.x, request.TargetPosition.y, request.TargetPosition.z));
            SetTargetEntity(context, request.HasTargetEntity, request.TargetEntity);
            return true;
        }
    }
}
