using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum SkillAdvanceResult : byte
{
    None = 0,
    Running = 1,
    Completed = 2,
    Interrupted = 3,
    Failed = 4,
}

public static class SkillExecutionUtility
{
    private static readonly SkillContent SkillContent = new();
    private static readonly List<SkillFollowupEffectData> PendingFollowupEffects = new();
    private static readonly SkillFactory s_skillFactory = CreateSkillFactory();

    public static bool PrepareCast(
        EntityManager entityManager,
        Entity entity,
        ref UnitCastComponent cast,
        int skillId,
        int skillAdditionId,
        ResolvedSkillData resolvedSkill,
        bool hasLockedTarget,
        float2 lockedTargetPosition)
    {
        ResetCastState(entityManager, entity, ref cast);
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = hasLockedTarget;
        cast.LockedTargetPosition = lockedTargetPosition;
        cast.CurrentSkillId = skillId;
        cast.CurrentSkillAdditionId = skillAdditionId;
        cast.HasPreparedCast = true;
        SetResolvedSkillPayload(entityManager, entity, resolvedSkill);

        if (skillId < 0 || resolvedSkill == null)
        {
            ResetCastState(entityManager, entity, ref cast);
            return false;
        }

        return true;
    }

    public static SkillAdvanceResult AdvanceCurrentSkill(EntityManager entityManager, Entity entity, float deltaTime, ref UnitCastComponent cast)
    {
        if (!cast.IsCasting)
            return SkillAdvanceResult.None;

        if (cast.ForceInterrupt)
        {
            InterruptCurrentSkill(entityManager, entity, ref cast);
            return SkillAdvanceResult.Interrupted;
        }

        float remainingTime = deltaTime;
        int guard = 0;

        while (cast.IsCasting && remainingTime >= 0f && guard++ < 16)
        {
            if (cast.IsWaitingHook)
            {
                SkillAdvanceResult hookResult = TickHookTasks(entityManager, entity, ref cast, ref remainingTime);
                if (hookResult != SkillAdvanceResult.Running)
                    return hookResult;

                if (remainingTime <= 0f)
                    return SkillAdvanceResult.Running;

                continue;
            }

            if (!TryGetCurrentSkill(entityManager, entity, cast, out ResolvedSkillData skillData))
            {
                InterruptCurrentSkill(entityManager, entity, ref cast);
                return SkillAdvanceResult.Failed;
            }

            float phaseDuration = GetPhaseDuration(skillData, cast.Phase);
            float phaseRemaining = math.max(phaseDuration - cast.PhaseElapsed, 0f);
            if (phaseRemaining > remainingTime && phaseRemaining > 0f)
            {
                cast.PhaseElapsed += remainingTime;
                return SkillAdvanceResult.Running;
            }

            cast.PhaseElapsed = phaseDuration;
            remainingTime = math.max(remainingTime - phaseRemaining, 0f);

            SkillAdvanceResult phaseResult = AdvancePhase(skillData, ref cast);
            if (phaseResult == SkillAdvanceResult.Failed)
            {
                InterruptCurrentSkill(entityManager, entity, ref cast);
                return SkillAdvanceResult.Failed;
            }

            if (phaseResult != SkillAdvanceResult.Running)
                return phaseResult;

            if (remainingTime <= 0f)
                return SkillAdvanceResult.Running;
        }

        return cast.IsCasting ? SkillAdvanceResult.Running : SkillAdvanceResult.Completed;
    }

    public static void ApplyMovement(EntityManager entityManager, Entity entity, in UnitCastComponent cast)
    {
        if (!entityManager.HasComponent<UnitMoveComponent>(entity))
            return;

        UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
        if (!cast.IsCasting)
        {
            move.StateSpeedFactor = 1f;
            entityManager.SetComponentData(entity, move);
            return;
        }

        move.AccelInput = float2.zero;
        move.StateSpeedFactor = 1f;

        if (TryGetCurrentSkill(entityManager, entity, cast, out ResolvedSkillData skillData) &&
            skillData.CanMoveWhileCasting &&
            entityManager.HasComponent<UnitIntentComponent>(entity))
        {
            UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
            move.AccelInput = intent.MoveDirection;
            move.StateSpeedFactor = math.max(0f, skillData.MoveSpeedMultiplier);
        }

        entityManager.SetComponentData(entity, move);
    }

    public static bool TryResolveCurrentSkill(EntityManager entityManager, Entity entity, in UnitCastComponent cast, out ResolvedSkillData skillData)
    {
        return TryGetCurrentSkill(entityManager, entity, cast, out skillData);
    }

    public static bool ExecuteResolvedSkillOnce(
        EntityManager entityManager,
        Entity entity,
        in UnitCastComponent cast,
        ResolvedSkillData skillData,
        SkillModifierSet runtimeModifiers = null)
    {
        if (skillData == null)
            return false;

        string runtimeType = SkillData.GetEffectiveRuntimeType(skillData.RuntimeType);
        Skill skill = s_skillFactory.CreateSkill(runtimeType, skillData);
        return skill != null && skill.TryExecute(entityManager, entity, cast, SkillContent, runtimeModifiers);
    }

    public static bool TryStartPreparedSkill(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, out ResolvedSkillData skillData)
    {
        skillData = null;

        if (!cast.HasPreparedCast || !TryGetCurrentSkill(entityManager, entity, cast, out skillData))
            return false;

        if (!TryConsumeMana(entityManager, entity, skillData.MpCost))
            return false;

        cast.ExecutionSerialCounter++;
        cast.CurrentExecutionToken = cast.ExecutionSerialCounter;
        cast.HasPreparedCast = false;
        cast.IsCasting = true;
        cast.StartedThisFrame = true;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = 0f;
        ResetTaskPayload(entityManager, entity, cast.CurrentExecutionToken);
        ScheduleHook(ref cast, SkillCastHookPoint.BeforeWindup, SkillCastHookContinuation.StartWindup);
        return true;
    }

    public static void ResetCastState(EntityManager entityManager, Entity entity, ref UnitCastComponent cast)
    {
        ClearExecutionState(entityManager, entity, ref cast);
        cast.HasPreparedCast = false;
        cast.HasLockedTarget = false;
        cast.LockedTargetPosition = float2.zero;
        cast.CurrentSkillId = -1;
        cast.CurrentSkillAdditionId = -1;
        ClearResolvedSkillPayload(entityManager, entity);
    }

    public static void ClearFollowupEffects(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasBuffer<UnitCastFollowupEffectElement>(entity))
            return;

        entityManager.GetBuffer<UnitCastFollowupEffectElement>(entity).Clear();
    }

    public static void ClearJumpArcState(EntityManager entityManager, Entity entity)
    {
        if (entityManager.Exists(entity) && entityManager.HasComponent<UnitJumpArcComponent>(entity))
            entityManager.RemoveComponent<UnitJumpArcComponent>(entity);

        if (entityManager.Exists(entity) && entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
            move.AccelInput = float2.zero;
            move.Velocity = float2.zero;
            entityManager.SetComponentData(entity, move);
        }

        if (entityManager.Exists(entity) && entityManager.HasComponent<Unity.Physics.PhysicsVelocity>(entity))
        {
            Unity.Physics.PhysicsVelocity physicsVelocity = entityManager.GetComponentData<Unity.Physics.PhysicsVelocity>(entity);
            physicsVelocity.Linear = float3.zero;
            physicsVelocity.Angular = float3.zero;
            entityManager.SetComponentData(entity, physicsVelocity);
        }
    }

    private static SkillAdvanceResult TickHookTasks(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
    {
        UnitCastTaskPayloadComponent payload = GetOrCreateTaskPayload(entityManager, entity);
        EnsureHookTasksInitialized(cast, payload);

        for (int i = payload.ActiveTasks.Count - 1; i >= 0; i--)
        {
            SkillCastTaskRuntime task = payload.ActiveTasks[i];
            if (task == null || task.HookPoint != cast.WaitingHookPoint)
                continue;

            if (task.Tick(entityManager, entity, ref cast, ref remainingTime))
                payload.ActiveTasks.RemoveAt(i);
        }

        if (HasPendingTasksForHook(payload, cast.WaitingHookPoint))
            return SkillAdvanceResult.Running;

        return ContinueAfterHook(entityManager, entity, ref cast);
    }

    private static void EnsureHookTasksInitialized(in UnitCastComponent cast, UnitCastTaskPayloadComponent payload)
    {
        if (payload == null || payload.ExecutionToken != cast.CurrentExecutionToken)
            return;

        int bit = 1 << (int)cast.WaitingHookPoint;
        if ((payload.InitializedHookMask & bit) != 0)
            return;

        payload.InitializedHookMask |= bit;
        SkillData baseSkillData = DataComponent.Instance?.Get<SkillData>(cast.CurrentSkillId);
        int additionId = GetCurrentSkillAdditionId(cast);
        SkillEffectData skillAdditionData = SkillChainResolver.GetSkillAdditionData(additionId);
        AppendHookTasks(payload, baseSkillData?.CastTasks, cast.WaitingHookPoint);
        AppendHookTasks(payload, skillAdditionData?.CastTasks, cast.WaitingHookPoint);
    }

    private static bool HasPendingTasksForHook(UnitCastTaskPayloadComponent payload, SkillCastHookPoint hookPoint)
    {
        if (payload?.ActiveTasks == null)
            return false;

        for (int i = 0; i < payload.ActiveTasks.Count; i++)
        {
            if (payload.ActiveTasks[i]?.HookPoint == hookPoint)
                return true;
        }

        return false;
    }

    private static SkillAdvanceResult ContinueAfterHook(EntityManager entityManager, Entity entity, ref UnitCastComponent cast)
    {
        SkillCastHookContinuation continuation = cast.HookContinuation;
        ClearHook(ref cast);

        switch (continuation)
        {
            case SkillCastHookContinuation.StartWindup:
                if (!TryGetCurrentSkill(entityManager, entity, cast, out ResolvedSkillData windupSkill))
                {
                    InterruptCurrentSkill(entityManager, entity, ref cast);
                    return SkillAdvanceResult.Failed;
                }

                cast.Phase = SkillCastPhase.Windup;
                cast.PhaseElapsed = 0f;
                cast.PhaseDuration = GetPhaseDuration(windupSkill, SkillCastPhase.Windup);
                LogPhase("Start Windup", cast);
                return SkillAdvanceResult.Running;

            case SkillCastHookContinuation.ScheduleBeforeExecute:
                ScheduleHook(ref cast, SkillCastHookPoint.BeforeExecute, SkillCastHookContinuation.ExecutePrimarySkill);
                return SkillAdvanceResult.Running;

            case SkillCastHookContinuation.ExecutePrimarySkill:
                if (!TryGetCurrentSkill(entityManager, entity, cast, out ResolvedSkillData executeSkill))
                {
                    InterruptCurrentSkill(entityManager, entity, ref cast);
                    return SkillAdvanceResult.Failed;
                }

                if (!TryExecuteSkill(entityManager, entity, cast, executeSkill))
                {
                    InterruptCurrentSkill(entityManager, entity, ref cast);
                    return SkillAdvanceResult.Failed;
                }

                ConsumeMatchingFollowupEffects(entityManager, entity, cast, executeSkill);
                AppendGeneratedFollowupEffects(entityManager, entity, cast);
                ScheduleHook(ref cast, SkillCastHookPoint.BeforeRecovery, SkillCastHookContinuation.StartRecovery);
                return SkillAdvanceResult.Running;

            case SkillCastHookContinuation.StartRecovery:
                if (!TryGetCurrentSkill(entityManager, entity, cast, out ResolvedSkillData recoverySkill))
                {
                    InterruptCurrentSkill(entityManager, entity, ref cast);
                    return SkillAdvanceResult.Failed;
                }

                cast.Phase = SkillCastPhase.Recovery;
                cast.PhaseElapsed = 0f;
                cast.PhaseDuration = GetPhaseDuration(recoverySkill, SkillCastPhase.Recovery);
                LogPhase("Start Recovery", cast);
                return SkillAdvanceResult.Running;

            case SkillCastHookContinuation.FinishSkill:
                CompleteCurrentSkill(entityManager, entity, ref cast);
                return SkillAdvanceResult.Completed;

            default:
                InterruptCurrentSkill(entityManager, entity, ref cast);
                return SkillAdvanceResult.Failed;
        }
    }

    private static SkillAdvanceResult AdvancePhase(ResolvedSkillData skillData, ref UnitCastComponent cast)
    {
        switch (cast.Phase)
        {
            case SkillCastPhase.Windup:
                cast.Phase = SkillCastPhase.Chanting;
                cast.PhaseElapsed = 0f;
                cast.PhaseDuration = GetPhaseDuration(skillData, SkillCastPhase.Chanting);
                LogPhase("Start Chanting", cast);
                return SkillAdvanceResult.Running;

            case SkillCastPhase.Chanting:
                ScheduleHook(ref cast, SkillCastHookPoint.BeforeChantEnd, SkillCastHookContinuation.ScheduleBeforeExecute);
                return SkillAdvanceResult.Running;

            case SkillCastPhase.Recovery:
                ScheduleHook(ref cast, SkillCastHookPoint.AfterRecovery, SkillCastHookContinuation.FinishSkill);
                return SkillAdvanceResult.Running;

            default:
                return SkillAdvanceResult.Failed;
        }
    }

    private static bool TryExecuteSkill(EntityManager entityManager, Entity entity, in UnitCastComponent cast, ResolvedSkillData skillData)
    {
        return ExecuteResolvedSkillOnce(entityManager, entity, cast, skillData);
    }

    private static SkillFactory CreateSkillFactory()
    {
        SkillFactory factory = new();
        SkillRegistry.RegisterAll(factory);
        return factory;
    }

    private static bool TryConsumeMana(EntityManager entityManager, Entity entity, int manaCost)
    {
        if (!entityManager.HasComponent<UnitManaComponent>(entity))
            return false;

        UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
        if (mana.CurrentMana < manaCost)
            return false;

        mana.CurrentMana -= manaCost;
        entityManager.SetComponentData(entity, mana);
        return true;
    }

    private static bool TryGetCurrentSkill(EntityManager entityManager, Entity entity, in UnitCastComponent cast, out ResolvedSkillData skillData)
    {
        skillData = null;
        if (cast.CurrentSkillId < 0 || !entityManager.HasComponent<UnitCastSkillPayloadComponent>(entity))
            return false;

        UnitCastSkillPayloadComponent payload = entityManager.GetComponentObject<UnitCastSkillPayloadComponent>(entity);
        skillData = payload?.ResolvedSkill;
        return skillData != null;
    }

    private static float GetPhaseDuration(ResolvedSkillData skillData, SkillCastPhase phase)
    {
        return phase switch
        {
            SkillCastPhase.Windup => skillData.WindupDuration,
            SkillCastPhase.Chanting => skillData.ChantDuration,
            SkillCastPhase.Recovery => skillData.RecoveryDuration,
            _ => 0f,
        };
    }

    private static void InterruptCurrentSkill(EntityManager entityManager, Entity entity, ref UnitCastComponent cast)
    {
        ClearExecutionState(entityManager, entity, ref cast);
    }

    private static void CompleteCurrentSkill(EntityManager entityManager, Entity entity, ref UnitCastComponent cast)
    {
        ClearExecutionState(entityManager, entity, ref cast);
    }

    private static void ClearExecutionState(EntityManager entityManager, Entity entity, ref UnitCastComponent cast)
    {
        UnitBuffUtility.RemoveRuntimeBuffsByExecutionToken(entityManager, entity, cast.CurrentExecutionToken);
        ResetTaskPayload(entityManager, entity, -1);
        ClearJumpArcState(entityManager, entity);
        cast.IsCasting = false;
        cast.StartedThisFrame = false;
        cast.ForceInterrupt = false;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = 0f;
        cast.CurrentExecutionToken = -1;
        ClearHook(ref cast);
    }

    private static UnitCastTaskPayloadComponent GetOrCreateTaskPayload(EntityManager entityManager, Entity entity)
    {
        if (entityManager.HasComponent<UnitCastTaskPayloadComponent>(entity))
            return entityManager.GetComponentObject<UnitCastTaskPayloadComponent>(entity);

        UnitCastTaskPayloadComponent payload = new();
        entityManager.AddComponentObject(entity, payload);
        return payload;
    }

    private static void ResetTaskPayload(EntityManager entityManager, Entity entity, int executionToken)
    {
        UnitCastTaskPayloadComponent payload = GetOrCreateTaskPayload(entityManager, entity);
        payload.ExecutionToken = executionToken;
        payload.InitializedHookMask = 0;
        payload.ActiveTasks.Clear();
    }

    private static UnitCastSkillPayloadComponent GetOrCreateSkillPayload(EntityManager entityManager, Entity entity)
    {
        if (entityManager.HasComponent<UnitCastSkillPayloadComponent>(entity))
            return entityManager.GetComponentObject<UnitCastSkillPayloadComponent>(entity);

        UnitCastSkillPayloadComponent payload = new();
        entityManager.AddComponentObject(entity, payload);
        return payload;
    }

    private static void SetResolvedSkillPayload(EntityManager entityManager, Entity entity, ResolvedSkillData resolvedSkill)
    {
        UnitCastSkillPayloadComponent payload = GetOrCreateSkillPayload(entityManager, entity);
        payload.ResolvedSkill = resolvedSkill;
    }

    private static void ClearResolvedSkillPayload(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<UnitCastSkillPayloadComponent>(entity))
            return;

        UnitCastSkillPayloadComponent payload = entityManager.GetComponentObject<UnitCastSkillPayloadComponent>(entity);
        if (payload != null)
            payload.ResolvedSkill = null;
    }

    private static void AppendHookTasks(UnitCastTaskPayloadComponent payload, List<SkillCastTaskData> taskDataList, SkillCastHookPoint hookPoint)
    {
        if (payload == null || taskDataList == null || taskDataList.Count == 0)
            return;

        for (int i = 0; i < taskDataList.Count; i++)
        {
            SkillCastTaskData taskData = taskDataList[i];
            if (taskData == null || taskData.HookPoint != hookPoint)
                continue;

            SkillCastTaskRuntime runtime = SkillCastTaskRuntimeFactory.Create(taskData);
            if (runtime != null)
                payload.ActiveTasks.Add(runtime);
        }
    }

    private static void ScheduleHook(ref UnitCastComponent cast, SkillCastHookPoint hookPoint, SkillCastHookContinuation continuation)
    {
        cast.IsWaitingHook = true;
        cast.WaitingHookPoint = hookPoint;
        cast.HookContinuation = continuation;
    }

    private static void ClearHook(ref UnitCastComponent cast)
    {
        cast.IsWaitingHook = false;
        cast.WaitingHookPoint = default;
        cast.HookContinuation = SkillCastHookContinuation.None;
    }

    private static void ConsumeMatchingFollowupEffects(EntityManager entityManager, Entity entity, in UnitCastComponent cast, ResolvedSkillData resolvedSkillData)
    {
        if (!entityManager.HasBuffer<UnitCastFollowupEffectElement>(entity))
            return;

        SkillData baseSkill = DataComponent.Instance?.Get<SkillData>(cast.CurrentSkillId);
        if (baseSkill == null)
            return;

        SkillChainSlotData slotData = GetCurrentSlotData(cast);
        SkillFollowupContext context = new(entityManager, entity, baseSkill, resolvedSkillData, slotData);
        DynamicBuffer<UnitCastFollowupEffectElement> followupEffects = entityManager.GetBuffer<UnitCastFollowupEffectElement>(entity);
        for (int i = followupEffects.Length - 1; i >= 0; i--)
        {
            UnitCastFollowupEffectElement followupEffect = followupEffects[i];
            if (!SkillResolver.MatchesFollowupEffect(followupEffect, baseSkill, slotData))
                continue;

            string consumeRuleKey = followupEffect.ConsumeRuleKey.ToString();
            if (!SkillFollowupConsumeRuleRegistry.TryCreateRule(consumeRuleKey, out SkillFollowupConsumeRule rule))
                continue;

            if (!rule.CanApply(followupEffect, context))
                continue;

            string modifierRuleKey = followupEffect.ModifierRuleKey.ToString();
            if (SkillFollowupModifierRuleRegistry.TryCreateRule(modifierRuleKey, out SkillFollowupModifierRule modifierRule))
                modifierRule.OnConsumed(ref followupEffect, context);

            if (!rule.Consume(ref followupEffect, context))
                followupEffects.RemoveAt(i);
            else
                followupEffects[i] = followupEffect;
        }
    }

    private static void AppendGeneratedFollowupEffects(EntityManager entityManager, Entity entity, in UnitCastComponent cast)
    {
        if (!entityManager.HasBuffer<UnitCastFollowupEffectElement>(entity))
            return;

        PendingFollowupEffects.Clear();

        SkillData baseSkill = DataComponent.Instance?.Get<SkillData>(cast.CurrentSkillId);
        if (baseSkill?.FollowupEffects != null && baseSkill.FollowupEffects.Count > 0)
            PendingFollowupEffects.AddRange(baseSkill.FollowupEffects);

        int additionId = GetCurrentSkillAdditionId(cast);
        if (additionId >= 0 &&
            DataComponent.Instance?.Get<SkillEffectData>(additionId) is SkillEffectData skillEffectData &&
            skillEffectData.FollowupEffects != null &&
            skillEffectData.FollowupEffects.Count > 0)
        {
            PendingFollowupEffects.AddRange(skillEffectData.FollowupEffects);
        }

        if (PendingFollowupEffects.Count == 0)
            return;

        DynamicBuffer<UnitCastFollowupEffectElement> followupBuffer = entityManager.GetBuffer<UnitCastFollowupEffectElement>(entity);
        for (int i = 0; i < PendingFollowupEffects.Count; i++)
        {
            if (TryCreateFollowupRuntime(PendingFollowupEffects[i], cast.CurrentSkillId, additionId, out UnitCastFollowupEffectElement followupEffect))
                followupBuffer.Add(followupEffect);
        }
    }

    private static SkillChainSlotData GetCurrentSlotData(in UnitCastComponent cast)
    {
        int additionId = GetCurrentSkillAdditionId(cast);
        return additionId >= 0 ? new SkillChainSlotData { SkillAdditionId = additionId } : null;
    }

    private static int GetCurrentSkillAdditionId(in UnitCastComponent cast)
    {
        return cast.CurrentSkillAdditionId;
    }

    private static bool TryCreateFollowupRuntime(SkillFollowupEffectData followupData, int sourceSkillId, int sourceSkillAdditionId, out UnitCastFollowupEffectElement followupEffect)
    {
        followupEffect = default;
        if (followupData == null)
            return false;

        followupData.EnsureDefaults();
        if (followupData.ConsumeRule == null || followupData.ModifierRule == null)
            return false;

        followupEffect.SourceSkillId = sourceSkillId;
        followupEffect.SourceSkillAdditionId = sourceSkillAdditionId;
        followupEffect.ConsumeRuleKey = SkillFollowupConsumeRuleRegistry.GetRuleKey(followupData.ConsumeRule);
        followupEffect.ModifierRuleKey = SkillFollowupModifierRuleRegistry.GetRuleKey(followupData.ModifierRule);
        followupEffect.FilterType = followupData.FilterType;
        followupEffect.SkillId = followupData.SkillId;
        followupEffect.RuntimeType = followupData.EffectiveRuntimeType;
        followupEffect.Element = followupData.Element;
        followupEffect.SkillAdditionId = followupData.SkillAdditionId;

        string consumeRuleKey = followupEffect.ConsumeRuleKey.ToString();
        if (!SkillFollowupConsumeRuleRegistry.TryCreateRule(consumeRuleKey, out SkillFollowupConsumeRule rule))
            return false;

        if (!rule.TryInitializeRuntime(followupData.ConsumeRule, ref followupEffect))
            return false;

        string modifierRuleKey = followupEffect.ModifierRuleKey.ToString();
        if (!SkillFollowupModifierRuleRegistry.TryCreateRule(modifierRuleKey, out SkillFollowupModifierRule modifierRule))
            return false;

        if (!modifierRule.TryInitializeRuntime(followupData.ModifierRule, ref followupEffect))
            return false;

        return followupEffect.ModifierSlices.Length > 0;
    }

    private static void LogPhase(string phaseName, in UnitCastComponent cast)
    {
        Debug.Log($"[CastState] {phaseName} | SkillId={cast.CurrentSkillId} Duration={cast.PhaseDuration}");
    }
}
