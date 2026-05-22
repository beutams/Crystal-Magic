using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;

[UpdateAfter(typeof(UnitStateTransitionSystem))]
[UpdateBefore(typeof(UnitSkillAnalysisSystem))]
partial class PlayerSkillAnalysisSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (castRef, requestRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>, RefRW<PlayerSkillComponent>>().WithAll<PlayerTag>().WithEntityAccess())
        {
            if (!requestRef.ValueRO.HasPendingCast || castRef.ValueRO.IsCasting)
                continue;

            UnitCastComponent cast = castRef.ValueRW;
            PlayerSkillComponent request = requestRef.ValueRW;
            TryStartPendingCast(EntityManager, entity, ref request, ref cast);
            castRef.ValueRW = cast;
            requestRef.ValueRW = request;
        }
    }

    public static bool TryQueueSelectedChainRequest(EntityManager entityManager, Entity entity, ref PlayerSkillComponent request)
    {
        request.Clear();

        if (!entityManager.HasComponent<UnitIntentComponent>(entity))
            return false;

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
        var slots = new System.Collections.Generic.List<SkillChainSlotData>();
        try
        {
            if (!SkillChainResolver.TryBuildSelectedChain(skillConfig, runtimeSkillData, slots, out int chainIndex))
                return false;

            for (int i = 0; i < slots.Count; i++)
            {
                SkillChainSlotData slotData = slots[i];
                SkillData skillData = SkillChainResolver.GetSkillData(slotData);
                if (skillData == null)
                    continue;

                if (request.SkillIds.Length >= request.SkillIds.Capacity ||
                    request.SkillAdditionIds.Length >= request.SkillAdditionIds.Capacity)
                    break;

                request.SkillIds.Add(skillData.Id);
                request.SkillAdditionIds.Add(slotData?.SkillAdditionId ?? -1);
            }

            if (request.SkillIds.Length == 0)
                return false;

            UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
            request.HasPendingCast = true;
            request.HasLockedTarget = true;
            request.LockedTargetPosition = intent.CastTargetPosition;
            request.ChainIndex = chainIndex;
            return true;
        }
        finally
        {
            slots.Clear();
        }
    }

    public static bool TryStartPendingCast(EntityManager entityManager, Entity entity, ref PlayerSkillComponent request, ref UnitCastComponent cast)
    {
        if (!request.HasPendingCast || request.SkillIds.Length == 0)
        {
            request.Clear();
            return false;
        }

        var resolvedSkills = new System.Collections.Generic.List<ResolvedSkillData>();
        try
        {
            for (int i = 0; i < request.SkillIds.Length; i++)
            {
                int skillId = request.SkillIds[i];
                int skillAdditionId = i < request.SkillAdditionIds.Length ? request.SkillAdditionIds[i] : -1;
                if (!SkillAnalysisUtility.TryAnalyzeSkill(entityManager, entity, skillId, skillAdditionId, out ResolvedSkillData resolvedSkill))
                {
                    request.Clear();
                    return false;
                }

                resolvedSkills.Add(resolvedSkill);
            }

            bool started = SkillExecutionUtility.TryBeginCast(
                entityManager,
                entity,
                ref cast,
                request.SkillIds,
                request.SkillAdditionIds,
                request.ChainIndex,
                resolvedSkills,
                request.HasLockedTarget,
                request.LockedTargetPosition);

            request.Clear();
            return started;
        }
        finally
        {
            resolvedSkills.Clear();
        }
    }
}
