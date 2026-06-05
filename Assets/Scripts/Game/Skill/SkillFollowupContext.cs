using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    public readonly struct SkillFollowupContext
    {
        public SkillFollowupContext(
            EntityManager entityManager,
            Entity entity,
            SkillData skillData,
            ResolvedSkillData resolvedSkillData,
            SkillAdditionData skillAdditionData)
        {
            EntityManager = entityManager;
            Entity = entity;
            SkillData = skillData;
            ResolvedSkillData = resolvedSkillData;
            SkillAdditionData = skillAdditionData;
        }

        public EntityManager EntityManager { get; }
        public Entity Entity { get; }
        public SkillData SkillData { get; }
        public ResolvedSkillData ResolvedSkillData { get; }
        public SkillAdditionData SkillAdditionData { get; }
    }
}
