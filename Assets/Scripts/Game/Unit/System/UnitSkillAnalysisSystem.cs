using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(PlayerSkillAnalysisSystem))]
[UpdateBefore(typeof(UnitSkillExecuteSystem))]
partial class UnitSkillAnalysisSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (castRef, unitSkillRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>, RefRW<UnitSkillComponent>>().WithNone<PlayerTag>().WithEntityAccess())
        {
            if (!unitSkillRef.ValueRO.HasPendingCast || castRef.ValueRO.IsCasting || castRef.ValueRO.HasPreparedCast)
                continue;

            UnitCastComponent cast = castRef.ValueRW;
            UnitSkillComponent unitSkill = unitSkillRef.ValueRW;
            TryPreparePendingCast(EntityManager, entity, ref unitSkill, ref cast);
            castRef.ValueRW = cast;
            unitSkillRef.ValueRW = unitSkill;
        }
    }

    public static bool TryPreparePendingCast(EntityManager entityManager, Entity entity, ref UnitSkillComponent unitSkill, ref UnitCastComponent cast)
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

        bool prepared = SkillExecutionUtility.PrepareCast(
            entityManager,
            entity,
            ref cast,
            entry.SkillId,
            entry.SkillAdditionId,
            resolvedSkill);

        if (prepared)
        {
            entry.CooldownRemaining = math.max(0f, entry.CooldownSeconds);
            unitSkill.Skills[index] = entry;
        }

        unitSkill.ClearPending();
        return prepared;
    }
}
