using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

[UpdateAfter(typeof(PlayerSkillAnalysisSystem))]
[UpdateBefore(typeof(UnitSkillExecuteSystem))]
partial class UnitSkillAnalysisSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (castRef, unitSkillRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>, RefRW<UnitSkillComponent>>().WithNone<PlayerTag>().WithEntityAccess())
        {
            if (!unitSkillRef.ValueRO.HasPendingCast || castRef.ValueRO.IsCasting)
                continue;

            UnitCastComponent cast = castRef.ValueRW;
            UnitSkillComponent unitSkill = unitSkillRef.ValueRW;
            TryStartPendingCast(EntityManager, entity, ref unitSkill, ref cast);
            castRef.ValueRW = cast;
            unitSkillRef.ValueRW = unitSkill;
        }
    }

    public static bool TryStartPendingCast(EntityManager entityManager, Entity entity, ref UnitSkillComponent unitSkill, ref UnitCastComponent cast)
    {
        int index = unitSkill.PendingSkillIndex;
        if (!unitSkill.HasPendingCast || index < 0 || index >= unitSkill.Skills.Length)
        {
            unitSkill.ClearPending();
            return false;
        }

        UnitSkillEntry entry = unitSkill.Skills[index];
        if (!SkillAnalysisUtility.TryAnalyzeSkill(entityManager, entity, entry.SkillId, entry.SkillAdditionId, out ResolvedSkillData resolvedSkill))
        {
            unitSkill.ClearPending();
            return false;
        }

        Unity.Collections.FixedList64Bytes<int> skillIds = default;
        Unity.Collections.FixedList64Bytes<int> skillAdditionIds = default;
        skillIds.Add(entry.SkillId);
        skillAdditionIds.Add(entry.SkillAdditionId);
        var resolvedSkills = new System.Collections.Generic.List<ResolvedSkillData> { resolvedSkill };
        try
        {
            bool started = SkillExecutionUtility.TryBeginCast(
                entityManager,
                entity,
                ref cast,
                skillIds,
                skillAdditionIds,
                -1,
                resolvedSkills,
                unitSkill.HasLockedTarget,
                unitSkill.LockedTargetPosition);

            if (started)
            {
                entry.CooldownRemaining = math.max(0f, entry.CooldownSeconds);
                unitSkill.Skills[index] = entry;
            }

            unitSkill.ClearPending();
            return started;
        }
        finally
        {
            resolvedSkills.Clear();
        }
    }
}
