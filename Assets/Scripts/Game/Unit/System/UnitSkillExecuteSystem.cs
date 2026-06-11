using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitSkillAnalysisSystem))]
[UpdateAfter(typeof(DungeonExitSystem))]
partial class UnitSkillExecuteSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (castRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>>().WithEntityAccess())
        {
            UnitCastComponent cast = castRef.ValueRW;

            if (!cast.IsCasting && cast.HasPreparedCast)
            {
                if (!SkillExecutionUtility.TryStartPreparedSkill(EntityManager, entity, ref cast, out _))
                {
                    ClearPlayerChainRequestIfPresent(entity);
                    SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                    SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);
                }
            }

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
                    if (EntityManager.HasComponent<PlayerTag>(entity) &&
                        EntityManager.HasComponent<PlayerSkillComponent>(entity))
                    {
                        PlayerSkillComponent request = EntityManager.GetComponentData<PlayerSkillComponent>(entity);
                        if (PlayerSkillAnalysisSystem.TryPrepareNextSkill(EntityManager, entity, ref request, ref cast))
                        {
                            EntityManager.SetComponentData(entity, request);
                            break;
                        }

                        SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                        SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);

                        if (TryRestartHeldPlayerCast(entity, ref request, ref cast))
                        {
                            EntityManager.SetComponentData(entity, request);
                            break;
                        }

                        EntityManager.SetComponentData(entity, request);
                    }

                    ClearPlayerChainRequestIfPresent(entity);
                    SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                    SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);
                    break;
                }

                case SkillAdvanceResult.Interrupted:
                case SkillAdvanceResult.Failed:
                    ClearPlayerChainRequestIfPresent(entity);
                    SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
                    SkillExecutionUtility.ClearFollowupEffects(EntityManager, entity);
                    break;
            }

            SkillExecutionUtility.ApplyMovement(EntityManager, entity, cast);
            castRef.ValueRW = cast;
        }
    }

    private bool TryRestartHeldPlayerCast(Entity entity, ref PlayerSkillComponent request, ref UnitCastComponent cast)
    {
        if (!EntityManager.HasComponent<UnitIntentComponent>(entity))
            return false;

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(entity);
        if (!intent.WantToCast)
            return false;

        if (!PlayerCastState.TryPopulateSelectedChainRequest(EntityManager, entity, ref request))
            return false;

        return PlayerSkillAnalysisSystem.TryPreparePendingCast(EntityManager, entity, ref request, ref cast);
    }

    private void ClearPlayerChainRequestIfPresent(Entity entity)
    {
        if (!EntityManager.HasComponent<PlayerSkillComponent>(entity))
            return;

        PlayerSkillComponent request = EntityManager.GetComponentData<PlayerSkillComponent>(entity);
        request.Clear();
        EntityManager.SetComponentData(entity, request);
    }
}
