using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitStateTransitionSystem))]
[UpdateBefore(typeof(UnitSkillAnalysisSystem))]
partial class PlayerSkillAnalysisSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (castRef, requestRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>, RefRW<PlayerSkillComponent>>().WithAll<PlayerTag>().WithEntityAccess())
        {
            if (!requestRef.ValueRO.HasPendingCast || castRef.ValueRO.IsCasting || castRef.ValueRO.HasPreparedCast)
                continue;

            UnitCastComponent cast = castRef.ValueRW;
            PlayerSkillComponent request = requestRef.ValueRW;
            TryPreparePendingCast(EntityManager, entity, ref request, ref cast);
            castRef.ValueRW = cast;
            requestRef.ValueRW = request;
        }
    }

    public static bool TryPreparePendingCast(EntityManager entityManager, Entity entity, ref PlayerSkillComponent request, ref UnitCastComponent cast)
    {
        if (!request.HasPendingCast || !request.HasActiveChain || request.SkillIds.Length == 0)
        {
            request.Clear();
            return false;
        }

        int skillIndex = request.CurrentSkillIndex;
        if (skillIndex < 0 || skillIndex >= request.SkillIds.Length)
        {
            request.Clear();
            return false;
        }

        int skillId = request.SkillIds[skillIndex];
        int skillAdditionId = skillIndex < request.SkillAdditionIds.Length ? request.SkillAdditionIds[skillIndex] : -1;
        if (!SkillAnalysisUtility.TryAnalyzeSkill(entityManager, entity, skillId, skillAdditionId, out ResolvedSkillData resolvedSkill))
        {
            request.Clear();
            return false;
        }

        bool prepared = SkillExecutionUtility.PrepareCast(
            entityManager,
            entity,
            ref cast,
            skillId,
            skillAdditionId,
            resolvedSkill);

        if (!prepared)
        {
            request.Clear();
            return false;
        }

        request.HasPendingCast = false;
        return true;
    }

    public static bool TryPrepareNextSkill(EntityManager entityManager, Entity entity, ref PlayerSkillComponent request, ref UnitCastComponent cast)
    {
        if (!request.HasActiveChain)
        {
            request.Clear();
            return false;
        }

        int nextIndex = request.CurrentSkillIndex + 1;
        if (nextIndex < 0 || nextIndex >= request.SkillIds.Length)
        {
            request.Clear();
            return false;
        }

        request.CurrentSkillIndex = nextIndex;
        request.HasPendingCast = true;
        return TryPreparePendingCast(entityManager, entity, ref request, ref cast);
    }
}
