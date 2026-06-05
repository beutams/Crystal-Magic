using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    public static class SkillAnalysisUtility
    {
        public static bool TryAnalyzeSkill(EntityManager entityManager, Entity entity, int skillId, int skillAdditionId, out ResolvedSkillData resolvedSkill)
        {
            resolvedSkill = null;

            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null || skillId < 0)
                return false;

            SkillData baseSkill = dataComponent.Get<SkillData>(skillId);
            if (baseSkill == null)
                return false;

            return TryAnalyzeSkill(entityManager, entity, baseSkill, skillAdditionId, out resolvedSkill);
        }

        public static bool TryAnalyzeSkill(EntityManager entityManager, Entity entity, SkillData baseSkill, int skillAdditionId, out ResolvedSkillData resolvedSkill)
        {
            resolvedSkill = null;
            if (baseSkill == null)
                return false;

            SkillAdditionData skillAdditionData = SkillChainResolver.GetSkillAdditionData(skillAdditionId);
            SkillModifierSet modifiers = SkillResolver.CollectModifiers(entityManager, entity, baseSkill, skillAdditionData);

            UnitAttackComponent? attack = entityManager.HasComponent<UnitAttackComponent>(entity)
                ? entityManager.GetComponentData<UnitAttackComponent>(entity)
                : null;
            UnitElementComponent? element = entityManager.HasComponent<UnitElementComponent>(entity)
                ? entityManager.GetComponentData<UnitElementComponent>(entity)
                : null;

            resolvedSkill = SkillResolver.Resolve(baseSkill, modifiers, skillAdditionData, attack, element);
            return resolvedSkill != null;
        }
    }
}
