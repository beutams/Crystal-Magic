using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Mathematics;
using UnityEngine;

[FactoryKey("CastState")]
public class CastState : AUnitState
{
    private readonly List<SkillChainSlotData> _skillSlots = new();
    private readonly SkillContent _skillContent = new();

    public override void OnEnter()
    {
        ResetCastState();

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
        if (!SkillChainResolver.TryBuildSelectedChain(skillConfig, runtimeSkillData, _skillSlots, out int chainIndex))
            return;

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);

        for (int i = 0; i < _skillSlots.Count; i++)
        {
            SkillChainSlotData slotData = _skillSlots[i];
            SkillData skillData = SkillChainResolver.GetSkillData(slotData);
            if (skillData == null)
                continue;

            cast.SkillIds.Add(skillData.Id);
            cast.SkillEffectIds.Add(slotData?.SkillEffectId ?? 0);
        }

        if (cast.SkillIds.Length == 0)
        {
            EntityManager.SetComponentData(Entity, cast);
            return;
        }

        cast.ForceInterrupt = false;
        cast.HasLockedTarget = intent.HasCastTarget;
        cast.LockedTargetPosition = intent.CastTargetPosition;
        cast.CurrentChainIndex = chainIndex;

        if (!TryStartSkill(ref cast, 0, out _))
        {
            InterruptCast(ref cast, "CannotStartFirstSkill");
            EntityManager.SetComponentData(Entity, cast);
            return;
        }

        EntityManager.SetComponentData(Entity, cast);
        EventComponent.Instance.Publish(new SkillCastLockChangedEvent(true));
        LogPhase("Start Windup", cast);
    }

    public override void OnUpdate(float deltaTime)
    {
        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        if (cast.IsCasting)
        {
            if (cast.ForceInterrupt)
            {
                InterruptCast(ref cast, "ForceInterrupt");
            }
            else if (!TryGetCurrentSkill(cast, out _))
            {
                InterruptCast(ref cast, "CurrentSkillMissing");
            }
            else
            {
                AdvanceCast(deltaTime, ref cast);
            }
        }

        ApplyMovement(cast);
        EntityManager.SetComponentData(Entity, cast);
    }

    public override void OnExit()
    {
        ApplyMovement(default);
    }

    private void AdvanceCast(float deltaTime, ref UnitCastComponent cast)
    {
        float remainingTime = deltaTime;
        int guard = 0;

        while (cast.IsCasting && remainingTime >= 0f && guard++ < 8)
        {
            if (!TryGetCurrentSkill(cast, out ResolvedSkillData skillData))
            {
                FinishCast(ref cast);
                break;
            }

            float phaseDuration = GetPhaseDuration(skillData, cast.Phase);
            float phaseRemaining = math.max(phaseDuration - cast.PhaseElapsed, 0f);

            if (phaseRemaining > remainingTime && phaseRemaining > 0f)
            {
                cast.PhaseElapsed += remainingTime;
                break;
            }

            cast.PhaseElapsed = phaseDuration;
            remainingTime = math.max(remainingTime - phaseRemaining, 0f);

            if (!AdvancePhase(skillData, ref cast))
                break;

            if (remainingTime <= 0f)
                break;
        }
    }

    private bool AdvancePhase(ResolvedSkillData skillData, ref UnitCastComponent cast)
    {
        switch (cast.Phase)
        {
            case SkillCastPhase.Windup:
                cast.Phase = SkillCastPhase.Chanting;
                cast.PhaseElapsed = 0f;
                cast.PhaseDuration = GetPhaseDuration(skillData, SkillCastPhase.Chanting);
                LogPhase("Start Chanting", cast);
                return true;

            case SkillCastPhase.Chanting:
                if (!TryExecuteSkill(skillData))
                {
                    InterruptCast(ref cast, "ExecuteFailed");
                    return false;
                }

                Debug.Log($"[CastState] Chanting Completed | Chain={cast.CurrentChainIndex} SkillIndex={cast.CurrentSkillIndex} SkillId={cast.CurrentSkillId}");
                cast.Phase = SkillCastPhase.Recovery;
                cast.PhaseElapsed = 0f;
                cast.PhaseDuration = GetPhaseDuration(skillData, SkillCastPhase.Recovery);
                LogPhase("Start Recovery", cast);
                return true;

            case SkillCastPhase.Recovery:
                int nextSkillIndex = cast.CurrentSkillIndex + 1;
                if (nextSkillIndex >= cast.SkillIds.Length)
                {
                    FinishCast(ref cast);
                    return false;
                }

                if (!TryStartSkill(ref cast, nextSkillIndex, out _))
                {
                    InterruptCast(ref cast, "CannotStartNextSkill");
                    return false;
                }

                LogPhase("Start Windup", cast);
                return true;

            default:
                FinishCast(ref cast);
                return false;
        }
    }

    private bool TryExecuteSkill(ResolvedSkillData skillData)
    {
        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        _skillContent.EntityManager = EntityManager;
        _skillContent.HasOriginEntity = true;
        _skillContent.OriginEntity = Entity;
        _skillContent.HasPosition = cast.HasLockedTarget;
        _skillContent.Position = new Vector3(cast.LockedTargetPosition.x, cast.LockedTargetPosition.y, 0f);
        _skillContent.HasTargetEntity = false;
        _skillContent.TargetEntity = Unity.Entities.Entity.Null;
        _skillContent.HasTarget = false;
        _skillContent.Target = null;
        _skillContent.Origin = null;

        SkillExecutor.ExecuteSkill(skillData, _skillContent);
        return true;
    }

    private bool TryStartSkill(ref UnitCastComponent cast, int skillIndex, out ResolvedSkillData skillData)
    {
        cast.CurrentSkillIndex = skillIndex;
        cast.CurrentSkillId = skillIndex >= 0 && skillIndex < cast.SkillIds.Length
            ? cast.SkillIds[skillIndex]
            : 0;

        if (!TryGetSkillByIndex(cast, skillIndex, out skillData))
        {
            return false;
        }

        if (!TryConsumeMana(skillData.MpCost))
        {
            return false;
        }

        cast.IsCasting = true;
        cast.Phase = SkillCastPhase.Windup;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = GetPhaseDuration(skillData, SkillCastPhase.Windup);
        return true;
    }

    private bool TryConsumeMana(int manaCost)
    {
        if (!EntityManager.HasComponent<UnitManaComponent>(Entity))
            return false;

        UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(Entity);
        if (mana.CurrentMana < manaCost)
            return false;

        mana.CurrentMana -= manaCost;
        EntityManager.SetComponentData(Entity, mana);
        return true;
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

    private void ApplyMovement(UnitCastComponent cast)
    {
        if (!EntityManager.HasComponent<UnitMoveComponent>(Entity))
            return;

        UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(Entity);
        move.AccelInput = float2.zero;
        move.StateSpeedFactor = 1f;

        if (cast.IsCasting &&
            TryGetCurrentSkill(cast, out ResolvedSkillData skillData) &&
            skillData.CanMoveWhileCasting &&
            EntityManager.HasComponent<UnitIntentComponent>(Entity))
        {
            UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
            move.AccelInput = intent.MoveDirection;
            move.StateSpeedFactor = math.max(0f, skillData.MoveSpeedMultiplier);
        }

        EntityManager.SetComponentData(Entity, move);
    }

    private bool TryGetCurrentSkill(UnitCastComponent cast, out ResolvedSkillData skillData)
    {
        return TryGetSkillByIndex(cast, cast.CurrentSkillIndex, out skillData);
    }

    private bool TryGetSkillByIndex(UnitCastComponent cast, int skillIndex, out ResolvedSkillData skillData)
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

        int effectId = skillIndex < cast.SkillEffectIds.Length ? cast.SkillEffectIds[skillIndex] : 0;
        SkillChainSlotData slotData = effectId > 0
            ? new SkillChainSlotData { SkillEffectId = effectId }
            : null;

        SkillModifierSet modifiers = SkillResolver.CollectModifiers(EntityManager, Entity, slotData);
        UnitAttackComponent? attack = EntityManager.HasComponent<UnitAttackComponent>(Entity)
            ? EntityManager.GetComponentData<UnitAttackComponent>(Entity)
            : null;
        UnitElementComponent? element = EntityManager.HasComponent<UnitElementComponent>(Entity)
            ? EntityManager.GetComponentData<UnitElementComponent>(Entity)
            : null;
        skillData = SkillResolver.Resolve(baseSkill, modifiers, attack, element);
        return skillData != null;
    }

    private void FinishCast(ref UnitCastComponent cast)
    {
        bool wasCasting = cast.IsCasting;
        cast.IsCasting = false;
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = false;
        cast.LockedTargetPosition = float2.zero;
        cast.CurrentChainIndex = -1;
        cast.CurrentSkillIndex = -1;
        cast.CurrentSkillId = 0;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = 0f;
        cast.SkillIds = default;
        cast.SkillEffectIds = default;

        if (wasCasting)
        {
            EventComponent.Instance.Publish(new SkillCastLockChangedEvent(false));
        }
    }

    private void InterruptCast(ref UnitCastComponent cast, string reason)
    {
        FinishCast(ref cast);
    }

    private static void LogPhase(string phaseName, UnitCastComponent cast)
    {
        Debug.Log($"[CastState] {phaseName} | Chain={cast.CurrentChainIndex} SkillIndex={cast.CurrentSkillIndex} SkillId={cast.CurrentSkillId} Duration={cast.PhaseDuration}");
    }

    private void ResetCastState()
    {
        _skillSlots.Clear();

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        cast.IsCasting = false;
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = false;
        cast.LockedTargetPosition = float2.zero;
        cast.CurrentChainIndex = -1;
        cast.CurrentSkillIndex = -1;
        cast.CurrentSkillId = 0;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
        cast.PhaseDuration = 0f;
        cast.SkillIds = default;
        cast.SkillEffectIds = default;
        EntityManager.SetComponentData(Entity, cast);
    }

}
