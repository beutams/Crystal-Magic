using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

public static class SkillReleaseUtility
{
    private static readonly SkillFactory s_skillFactory = CreateSkillFactory();

    public static bool TryExecute(
        EntityManager entityManager,
        SkillReleaseRequest request,
        ResolvedSkillData resolvedSkill,
        SkillContent context)
    {
        if (request == null || resolvedSkill == null)
            return false;

        Skill skill = s_skillFactory.CreateSkill(SkillData.GetEffectiveRuntimeType(resolvedSkill.RuntimeType), resolvedSkill);
        return skill != null && skill.TryExecute(entityManager, request, context);
    }

    private static SkillFactory CreateSkillFactory()
    {
        SkillFactory factory = new();
        SkillRegistry.RegisterAll(factory);
        return factory;
    }
}
