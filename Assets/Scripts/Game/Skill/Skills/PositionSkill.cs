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
            bool hasTargetPosition = SkillTargetUtility.TryGetTargetPosition(entityManager, entity, out Unity.Mathematics.float2 targetPosition);
            SetPosition(
                context,
                hasTargetPosition,
                new Vector3(targetPosition.x, targetPosition.y, 0f));
            SetTargetEntity(context, false, Entity.Null);
            return true;
        }
    }
}
