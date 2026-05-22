using Unity.Entities;

[UpdateAfter(typeof(UnitSkillAnalysisSystem))]
[UpdateAfter(typeof(UnitSkillSystem))]
[UpdateAfter(typeof(UnitStateTransitionSystem))]
partial class UnitSkillExecuteSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (castRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>>().WithEntityAccess())
        {
            UnitCastComponent cast = castRef.ValueRW;

            if (cast.StartedThisFrame)
            {
                cast.StartedThisFrame = false;
                SkillExecutionUtility.ApplyMovement(EntityManager, entity, cast);
                castRef.ValueRW = cast;
                continue;
            }

            SkillAdvanceResult result = SkillAdvanceResult.None;
            if (cast.IsCasting)
                result = SkillExecutionUtility.AdvanceCurrentSkill(EntityManager, entity, deltaTime, ref cast);

            switch (result)
            {
                case SkillAdvanceResult.Completed:
                {
                    int nextSkillIndex = cast.CurrentSkillIndex + 1;
                    if (nextSkillIndex < cast.SkillIds.Length)
                    {
                        if (!SkillExecutionUtility.TryStartSkillAtIndex(EntityManager, entity, ref cast, nextSkillIndex, out _))
                        {
                            SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                            SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);
                        }
                    }
                    else if (EntityManager.HasComponent<PlayerTag>(entity) && TryRestartHeldPlayerCast(entity, ref cast))
                    {
                    }
                    else
                    {
                        SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                        SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);
                    }

                    break;
                }

                case SkillAdvanceResult.Interrupted:
                case SkillAdvanceResult.Failed:
                    SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                    SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);
                    break;
            }

            SkillExecutionUtility.ApplyMovement(EntityManager, entity, cast);
            castRef.ValueRW = cast;
        }
    }

    private bool TryRestartHeldPlayerCast(Entity entity, ref UnitCastComponent cast)
    {
        if (!EntityManager.HasComponent<UnitIntentComponent>(entity) || !EntityManager.HasComponent<PlayerSkillComponent>(entity))
            return false;

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(entity);
        if (!intent.WantToCast)
            return false;

        PlayerSkillComponent request = EntityManager.GetComponentData<PlayerSkillComponent>(entity);
        if (!PlayerSkillAnalysisSystem.TryQueueSelectedChainRequest(EntityManager, entity, ref request))
        {
            EntityManager.SetComponentData(entity, request);
            return false;
        }

        bool started = PlayerSkillAnalysisSystem.TryStartPendingCast(EntityManager, entity, ref request, ref cast);
        EntityManager.SetComponentData(entity, request);
        return started;
    }
}
