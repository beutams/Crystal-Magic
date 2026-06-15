using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Mathematics;
using UnityEngine;

[FactoryKey("UnitCastState")]
public class UnitCastState : AUnitState
{
    public override void OnEnter()
    {
        if (!EntityManager.HasComponent<UnitSkillComponent>(Entity) ||
            !EntityManager.HasComponent<UnitIntentComponent>(Entity) ||
            !EntityManager.HasComponent<UnitPerceptionComponent>(Entity) ||
            !EntityManager.HasComponent<UnitCastComponent>(Entity))
            return;

        UnitSkillComponent unitSkill = EntityManager.GetComponentData<UnitSkillComponent>(Entity);
        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        UnitPerceptionComponent perception = EntityManager.GetComponentData<UnitPerceptionComponent>(Entity);
        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);

        int selectedIndex = SelectSkillIndex(intent, unitSkill, perception.TargetDistance);
        if (selectedIndex < 0)
        {
            SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
            SkillExecutionUtility.ClearFollowupEffects(EntityManager, Entity);
            EntityManager.SetComponentData(Entity, cast);
            EntityManager.SetComponentData(Entity, unitSkill);
            return;
        }

        SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
        unitSkill.HasPendingCast = true;
        unitSkill.PendingSkillIndex = selectedIndex;
        EntityManager.SetComponentData(Entity, cast);
        EntityManager.SetComponentData(Entity, unitSkill);
        if (SkillTargetUtility.TryGetTargetPosition(EntityManager, Entity, out float2 targetPosition))
            UpdateAnimationFacing(targetPosition);
    }

    public override void OnUpdate(float deltaTime)
    {
        if (SkillTargetUtility.TryGetTargetPosition(EntityManager, Entity, out float2 targetPosition))
            UpdateAnimationFacing(targetPosition);
        else
            ClearAnimationFacingDirection();
    }

    public override void OnExit()
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity))
            return;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
        SkillExecutionUtility.ClearFollowupEffects(EntityManager, Entity);
        SkillExecutionUtility.ApplyMovement(EntityManager, Entity, cast);
        EntityManager.SetComponentData(Entity, cast);

        if (EntityManager.HasComponent<UnitSkillComponent>(Entity))
        {
            UnitSkillComponent unitSkill = EntityManager.GetComponentData<UnitSkillComponent>(Entity);
            unitSkill.ClearPending();
            EntityManager.SetComponentData(Entity, unitSkill);
        }

        ClearAnimationFacingDirection();
    }

    private void UpdateAnimationFacing(float2 targetPosition)
    {
        if (!EntityManager.HasComponent<Unity.Transforms.LocalTransform>(Entity))
            return;

        float2 selfPosition = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(Entity).Position.xy;
        float2 direction = targetPosition - selfPosition;
        if (math.lengthsq(direction) <= 0.0001f)
        {
            ClearAnimationFacingDirection();
            return;
        }

        SetAnimationFacingDirection(direction);
    }

    private int SelectSkillIndex(UnitIntentComponent intent, UnitSkillComponent unitSkill, float targetDistance)
    {
        int totalWeight = 0;
        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            UnitSkillEntry entry = unitSkill.Skills[i];
            if (!CanUseSkill(intent, entry, targetDistance, out _))
                continue;

            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (totalWeight <= 0)
            return -1;

        if (intent.SkillRequestMode == UnitSkillSelectionMode.ExactSkillId)
        {
            for (int i = 0; i < unitSkill.Skills.Length; i++)
            {
                UnitSkillEntry entry = unitSkill.Skills[i];
                if (CanUseSkill(intent, entry, targetDistance, out _))
                    return i;
            }

            return -1;
        }

        int random = UnityEngine.Random.Range(0, totalWeight);
        int accum = 0;
        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            UnitSkillEntry entry = unitSkill.Skills[i];
            if (!CanUseSkill(intent, entry, targetDistance, out _))
                continue;

            accum += Mathf.Max(1, entry.Weight);
            if (random < accum)
                return i;
        }

        return -1;
    }

    private bool CanUseSkill(UnitIntentComponent intent, UnitSkillEntry entry, float targetDistance, out ResolvedSkillData resolvedSkill)
    {
        if (entry.SkillId < 0)
        {
            resolvedSkill = null;
            return false;
        }
        if (entry.CooldownRemaining > 0f)
        {
            resolvedSkill = null;
            return false;
        }
        if (targetDistance < math.max(0f, entry.MinDistance))
        {
            resolvedSkill = null;
            return false;
        }
        if (entry.MaxDistance > 0f && targetDistance > entry.MaxDistance)
        {
            resolvedSkill = null;
            return false;
        }

        if (!TryResolveSkill(entry, out resolvedSkill))
            return false;

        if (EntityManager.HasComponent<UnitManaComponent>(Entity))
        {
            UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(Entity);
            if (mana.CurrentMana < resolvedSkill.MpCost)
                return false;
        }

        return intent.SkillRequestMode switch
        {
            UnitSkillSelectionMode.None => false,
            UnitSkillSelectionMode.RandomAll => true,
            UnitSkillSelectionMode.RandomTagMask => intent.RequestedTagMask != 0 && (entry.TagMask & intent.RequestedTagMask) != 0,
            UnitSkillSelectionMode.ExactSkillId => entry.SkillId == intent.RequestedSkillId,
            _ => false,
        };
    }

    private bool TryResolveSkill(UnitSkillEntry entry, out ResolvedSkillData resolvedSkill)
    {
        return SkillAnalysisUtility.TryAnalyzeSkill(EntityManager, Entity, entry.SkillId, -1, out resolvedSkill);
    }
}
