using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

public static class SkillReleaseSnapshotUtility
{
    public static bool TryCreate(
        EntityManager entityManager,
        SkillReleaseRequest request,
        out ResolvedSkillData resolvedSkill)
    {
        resolvedSkill = null;
        if (request == null || request.SkillId < 0)
            return false;

        SkillData baseSkill = DataComponent.Instance?.Get<SkillData>(request.SkillId);
        if (baseSkill == null)
            return false;

        SkillModifierSet finalModifiers = UnitModifierResolver.BuildPersistentSkillModifiers(
            entityManager,
            request.OriginEntity);
        finalModifiers.Add(request.ExtraModifiers);

        UnitElementComponent? element = UnitModifierResolver.TryCaptureElementState(
            entityManager,
            request.OriginEntity,
            out UnitElementComponent capturedElement)
            ? capturedElement
            : null;
        resolvedSkill = SkillResolver.Resolve(baseSkill, finalModifiers, element);
        return resolvedSkill != null;
    }
}
