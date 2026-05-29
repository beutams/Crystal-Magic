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

        protected override bool BuildContext(EntityManager entityManager, Entity entity, in UnitCastComponent cast, SkillContent context)
        {
            SetPosition(
                context,
                cast.HasLockedTarget,
                new Vector3(cast.LockedTargetPosition.x, cast.LockedTargetPosition.y, 0f));
            SetTargetEntity(context, false, Entity.Null);
            return true;
        }
    }
}
