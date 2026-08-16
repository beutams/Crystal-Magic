using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public static class SkillAnalysisUtility
    {
        public static bool TryAnalyzeSkill(in SkillReleaseRequest request, out ResolvedSkillData resolvedSkill)
        {
            resolvedSkill = null;

            DataComponent dataComponent = DataComponent.Instance;
            if (dataComponent == null || request == null || request.SkillId < 0)
                return false;

            SkillData baseSkill = dataComponent.Get<SkillData>(request.SkillId);
            if (baseSkill == null)
                return false;

            SkillModifierSet modifiers = request.ModifierSnapshot?.Clone() ?? new SkillModifierSet();

            UnitAttackComponent? attack = request.HasAttackSnapshot ? request.AttackSnapshot : null;
            UnitElementComponent? element = request.HasElementSnapshot ? request.ElementSnapshot : null;
            resolvedSkill = SkillResolver.Resolve(baseSkill, modifiers, attack, element);
            return resolvedSkill != null;
        }
    }
}
