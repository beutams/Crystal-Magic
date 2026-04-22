using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Mathematics;
using UnityEngine;

public class CastState : AUnitState
{
    private readonly List<SkillData> _skillConfigs = new();
    private readonly List<ResolvedSkillData> _skills = new();
    private readonly SkillContent _skillContent = new();

    public override void OnEnter()
    {
        ResetCastState();

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
        if (!SkillChainResolver.TryBuildSelectedChain(skillConfig, runtimeSkillData, _skillConfigs, out int chainIndex))
            return;

        SkillModifierSet modifiers = SkillResolver.CollectModifiers(EntityManager, Entity);
        for (int i = 0; i < _skillConfigs.Count; i++)
        {
            ResolvedSkillData resolvedSkill = SkillResolver.Resolve(_skillConfigs[i], modifiers);
            if (resolvedSkill != null)
                _skills.Add(resolvedSkill);
        }

        if (_skills.Count == 0)
            return;

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        cast.IsCasting = true;
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = intent.HasCastTarget;
        cast.LockedTargetPosition = intent.CastTargetPosition;
        cast.CurrentChainIndex = chainIndex;
        cast.CurrentSkillIndex = 0;
        cast.CurrentSkillId = _skills[0].Id;
        cast.Phase = SkillCastPhase.Windup;
        cast.PhaseElapsed = 0f;
        EntityManager.SetComponentData(Entity, cast);
    }

    public override void OnUpdate(float deltaTime)
    {
        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        if (cast.IsCasting)
        {
            if (cast.ForceInterrupt || !TryGetCurrentSkill(cast.CurrentSkillIndex, out _))
                InterruptCast(ref cast);
            else
                AdvanceCast(deltaTime, ref cast);
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
            if (!TryGetCurrentSkill(cast.CurrentSkillIndex, out ResolvedSkillData skillData))
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
                return true;

            case SkillCastPhase.Chanting:
                if (!TryExecuteSkill(skillData))
                {
                    InterruptCast(ref cast);
                    return false;
                }

                cast.Phase = SkillCastPhase.Recovery;
                cast.PhaseElapsed = 0f;
                return true;

            case SkillCastPhase.Recovery:
                int nextSkillIndex = cast.CurrentSkillIndex + 1;
                if (nextSkillIndex >= _skills.Count)
                {
                    FinishCast(ref cast);
                    return false;
                }

                cast.CurrentSkillIndex = nextSkillIndex;
                cast.CurrentSkillId = _skills[nextSkillIndex].Id;
                cast.Phase = SkillCastPhase.Windup;
                cast.PhaseElapsed = 0f;
                return true;

            default:
                FinishCast(ref cast);
                return false;
        }
    }

    private bool TryExecuteSkill(ResolvedSkillData skillData)
    {
        if (!TryConsumeMana(skillData.MpCost))
            return false;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        _skillContent.HasPosition = cast.HasLockedTarget;
        _skillContent.Position = new Vector3(cast.LockedTargetPosition.x, cast.LockedTargetPosition.y, 0f);
        _skillContent.HasTarget = false;
        _skillContent.Target = null;
        _skillContent.Origin = null;

        SkillExecutor.ExecuteSkill(skillData, _skillContent);
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

    private float GetPhaseDuration(ResolvedSkillData skillData, SkillCastPhase phase)
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
            TryGetCurrentSkill(cast.CurrentSkillIndex, out ResolvedSkillData skillData) &&
            skillData.CanMoveWhileCasting &&
            EntityManager.HasComponent<UnitIntentComponent>(Entity))
        {
            UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
            move.AccelInput = intent.MoveDirection;
            move.StateSpeedFactor = math.max(0f, skillData.MoveSpeedMultiplier);
        }

        EntityManager.SetComponentData(Entity, move);
    }

    private bool TryGetCurrentSkill(int skillIndex, out ResolvedSkillData skillData)
    {
        if (skillIndex >= 0 && skillIndex < _skills.Count)
        {
            skillData = _skills[skillIndex];
            return skillData != null;
        }

        skillData = null;
        return false;
    }

    private void FinishCast(ref UnitCastComponent cast)
    {
        cast.IsCasting = false;
        cast.ForceInterrupt = false;
        cast.HasLockedTarget = false;
        cast.LockedTargetPosition = float2.zero;
        cast.CurrentChainIndex = -1;
        cast.CurrentSkillIndex = -1;
        cast.CurrentSkillId = 0;
        cast.Phase = SkillCastPhase.None;
        cast.PhaseElapsed = 0f;
    }

    private void InterruptCast(ref UnitCastComponent cast)
    {
        FinishCast(ref cast);
    }

    private void ResetCastState()
    {
        _skillConfigs.Clear();
        _skills.Clear();

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
        EntityManager.SetComponentData(Entity, cast);
    }
}
