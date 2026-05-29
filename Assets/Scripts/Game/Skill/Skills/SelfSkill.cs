using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Transforms;

namespace CrystalMagic.Game.Skill
{
    [FactoryKey(nameof(SelfSkill), 10, "Self Skill")]
    public sealed class SelfSkill : Skill
    {
        public SelfSkill(ResolvedSkillData data) : base(data)
        {
        }

        protected override bool BuildContext(EntityManager entityManager, Entity entity, in UnitCastComponent cast, SkillContent context)
        {
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                SetPosition(context, true, transform.Position);
            }
            else
            {
                SetPosition(context, false, UnityEngine.Vector3.zero);
            }

            SetTargetEntity(context, true, entity);
            return true;
        }
    }
}
