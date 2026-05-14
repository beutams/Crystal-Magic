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

    public static bool TryBeginCast(
        EntityManager entityManager,
        Entity entity,
        ref UnitCastComponent cast,
        in Unity.Collections.FixedList64Bytes<int> skillIds,
        in Unity.Collections.FixedList64Bytes<int> skillAdditionIds,
        int chainIndex,
        bool hasLockedTarget,
        float2 lockedTargetPosition)
    {
        ResetCastState(ref cast);
        cast.SkillIds = skillIds;
        cast.SkillAdditionIds = skillAdditionIds;
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = hasLockedTarget;
        cast.LockedTargetPosition = lockedTargetPosition;
        cast.CurrentChainIndex = chainIndex;

        if (cast.SkillIds.Length == 0)
            return false;

        if (!TryStartSkillAtIndex(entityManager, entity, ref cast, 0, out _))
        {
            ResetCastState(ref cast);
            return false;
        }

        LogPhase("Start Windup", cast);
        return true;
    }

    public static SkillAdvanceResult AdvanceCurrentSkill(EntityManager entityManager, Entity entity, float deltaTime, ref UnitCastComponent cast)
    {
        if (!cast.IsCasting)
            return SkillAdvanceResult.None;

        if (cast.ForceInterrupt)
        {
            InterruptCurrentSkill(ref cast);
            return SkillAdvanceResult.Interrupted;
        }

        if (!TryGetCurrentSkill(entityManager, entity, cast, out _))
        {
            InterruptCurrentSkill(ref cast);
            return SkillAdvanceResult.Failed;
        }

        float remainingTime = deltaTime;
        int guard = 0;

        while (cast.IsCasting && remainingTime >= 0f && guard++ < 8)
        {
            if (!TryGetCurrentSkill(entityManager, entity, cast, out ResolvedSkillData skillData))
            {
                InterruptCurrentSkill(ref cast);
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

            SkillAdvanceResult phaseResult = AdvancePhase(entityManager, entity, skillData, ref cast);
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

    private static SkillAdvanceResult AdvancePhase(EntityManager entityManager, Entity entity, ResolvedSkillData skillData, ref UnitCastComponent cast)
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
                if (!TryExecuteSkill(entityManager, entity, cast, skillData))
                {
                    InterruptCurrentSkill(ref cast);
                    return SkillAdvanceResult.Failed;
                }

                Debug.Log($"[CastState] Chanting Completed | Chain={cast.CurrentChainIndex} SkillIndex={cast.CurrentSkillIndex} SkillId={cast.CurrentSkillId}");
                cast.Phase = SkillCastPhase.Recovery;
                cast.PhaseElapsed = 0f;
                cast.PhaseDuration = GetPhaseDuration(skillData, SkillCastPhase.Recovery);
                LogPhase("Start Recovery", cast);
                return SkillAdvanceResult.Running;

            case SkillCastPhase.Recovery:
                CompleteCurrentSkill(ref cast);
                return SkillAdvanceResult.Completed;

            default:
                InterruptCurrentSkill(ref cast);
                return SkillAdvanceResult.Failed;
        }
    }

    private static bool TryExecuteSkill(EntityManager entityManager, Entity entity, in UnitCastComponent cast, ResolvedSkillData skillData)
    {
        SkillContent.EntityManager = entityManager;
        SkillContent.HasOriginEntity = true;
        SkillContent.OriginEntity = entity;
        SetSkillReleasePosition(entityManager, entity, cast, skillData);
        SkillContent.HasTargetEntity = false;
        SkillContent.TargetEntity = Entity.Null;
        SkillContent.HasTarget = false;
        SkillContent.Target = null;
        SkillContent.Origin = null;
        SkillContent.RuntimeModifiers = new SkillModifierSet();

        SkillExecutor.ExecuteSkill(skillData, SkillContent);
        return true;
    }

    private static void SetSkillReleasePosition(EntityManager entityManager, Entity entity, in UnitCastComponent cast, ResolvedSkillData skillData)
    {
        switch (skillData?.SkillType)
        {
            case SkillType.SelfSkill:
                if (entityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
                {
                    Unity.Transforms.LocalTransform transform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                    SkillContent.HasPosition = true;
                    SkillContent.Position = transform.Position;
                }
                else
                {
                    SkillContent.HasPosition = false;
                    SkillContent.Position = Vector3.zero;
                }

                break;

            case SkillType.PositionSkill:
            default:
                SkillContent.HasPosition = cast.HasLockedTarget;
                SkillContent.Position = new Vector3(cast.LockedTargetPosition.x, cast.LockedTargetPosition.y, 0f);
                break;
        }
    }

    public static bool TryStartSkillAtIndex(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, int skillIndex, out ResolvedSkillData skillData)
    {
        cast.CurrentSkillIndex = skillIndex;
        cast.CurrentSkillId = skillIndex >= 0 && skillIndex < cast.SkillIds.Length
            ? cast.SkillIds[skillIndex]
            : -1;

        if (!TryGetSkillByIndex(entityManager, entity, cast, skillIndex, out skillData))
            return false;

        if (!TryConsumeMana(entityManager, entity, skillData.MpCost))
            return false;

        cast.IsCasting = true;
        cast.Phase = SkillCastPhase.Windup;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = GetPhaseDuration(skillData, SkillCastPhase.Windup);
        return true;
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
        return TryGetSkillByIndex(entityManager, entity, cast, cast.CurrentSkillIndex, out skillData);
    }

    private static bool TryGetSkillByIndex(EntityManager entityManager, Entity entity, in UnitCastComponent cast, int skillIndex, out ResolvedSkillData skillData)
    {
        if (skillIndex < 0 || skillIndex >= cast.SkillIds.Length)
        {
            skillData = null;
            return false;
        }

        DataComponent dataComponent = DataComponent.Instance;
        if (dataComponent == null)
        {
            skillData = null;
            return false;
        }

        SkillData baseSkill = dataComponent.Get<SkillData>(cast.SkillIds[skillIndex]);
        if (baseSkill == null)
        {
            skillData = null;
            return false;
        }

        int additionId = skillIndex < cast.SkillAdditionIds.Length ? cast.SkillAdditionIds[skillIndex] : -1;
        SkillChainSlotData slotData = additionId >= 0
            ? new SkillChainSlotData { SkillAdditionId = additionId }
            : null;

        SkillModifierSet modifiers = SkillResolver.CollectModifiers(entityManager, entity, slotData);
        UnitAttackComponent? attack = entityManager.HasComponent<UnitAttackComponent>(entity)
            ? entityManager.GetComponentData<UnitAttackComponent>(entity)
            : null;
        UnitElementComponent? element = entityManager.HasComponent<UnitElementComponent>(entity)
            ? entityManager.GetComponentData<UnitElementComponent>(entity)
            : null;
        skillData = SkillResolver.Resolve(baseSkill, modifiers, attack, element);
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

    private static void InterruptCurrentSkill(ref UnitCastComponent cast)
    {
        CompleteCurrentSkill(ref cast);
    }

    private static void CompleteCurrentSkill(ref UnitCastComponent cast)
    {
        cast.IsCasting = false;
        cast.ForceInterrupt = false;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = 0f;
    }

    public static void ResetCastState(ref UnitCastComponent cast)
    {
        cast.IsCasting = false;
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = false;
        cast.LockedTargetPosition = float2.zero;
        cast.CurrentChainIndex = -1;
        cast.CurrentSkillIndex = -1;
        cast.CurrentSkillId = -1;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = 0f;
        cast.SkillIds = default;
        cast.SkillAdditionIds = default;
    }

    private static void LogPhase(string phaseName, in UnitCastComponent cast)
    {
        Debug.Log($"[CastState] {phaseName} | Chain={cast.CurrentChainIndex} SkillIndex={cast.CurrentSkillIndex} SkillId={cast.CurrentSkillId} Duration={cast.PhaseDuration}");
    }
}
