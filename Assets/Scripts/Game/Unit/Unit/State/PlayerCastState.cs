using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;

[FactoryKey("PlayerCastState")]
public class PlayerCastState : AUnitState
{
    public override void OnEnter()
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity) ||
            !EntityManager.HasComponent<PlayerSkillComponent>(Entity))
            return;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        PlayerSkillComponent request = EntityManager.GetComponentData<PlayerSkillComponent>(Entity);
        if (TryPopulateSelectedChainRequest(EntityManager, Entity, ref request))
            EventComponent.Instance.Publish(new SkillCastLockChangedEvent(true));

        EntityManager.SetComponentData(Entity, cast);
        EntityManager.SetComponentData(Entity, request);
        UpdateAnimationFacing(request.LockedTargetPosition, request.HasLockedTarget);
    }

    public override void OnUpdate(float deltaTime)
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity) ||
            !EntityManager.HasComponent<PlayerSkillComponent>(Entity))
        {
            return;
        }

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        if (cast.HasLockedTarget)
        {
            UpdateAnimationFacing(cast.LockedTargetPosition, true);
            return;
        }

        PlayerSkillComponent request = EntityManager.GetComponentData<PlayerSkillComponent>(Entity);
        UpdateAnimationFacing(request.LockedTargetPosition, request.HasLockedTarget);
    }

    public override void OnExit()
    {
        if (EntityManager.HasComponent<UnitCastComponent>(Entity))
        {
            UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
            SkillExecutionUtility.ResetCastState(EntityManager, Entity, ref cast);
            SkillExecutionUtility.ClearFollowupEffects(EntityManager, Entity);
            SkillExecutionUtility.ApplyMovement(EntityManager, Entity, cast);
            EntityManager.SetComponentData(Entity, cast);
        }

        if (EntityManager.HasComponent<PlayerSkillComponent>(Entity))
        {
            PlayerSkillComponent request = EntityManager.GetComponentData<PlayerSkillComponent>(Entity);
            request.Clear();
            EntityManager.SetComponentData(Entity, request);
        }

        EventComponent.Instance.Publish(new SkillCastLockChangedEvent(false));
        ClearAnimationFacingDirection();
    }

    public static bool TryPopulateSelectedChainRequest(EntityManager entityManager, Entity entity, ref PlayerSkillComponent request)
    {
        request.Clear();

        if (!entityManager.HasComponent<UnitIntentComponent>(entity))
            return false;

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
            return false;

        int chainIndex = Mathf.Clamp(RuntimeDataComponent.Instance.GetSkillData().CurrentSkillChainIndex, 0, skillConfig.Chains.Length - 1);
        SkillChainData chain = skillConfig.Chains[chainIndex];
        chain?.EnsureSlots();
        if (chain?.Slots == null || chain.Slots.Count == 0)
            return false;

        for (int i = 0; i < chain.Slots.Count; i++)
        {
            SkillChainSlotData slotData = chain.Slots[i];
            if (slotData == null || slotData.SkillStoneItemId < 0)
                continue;

            SkillData skillData = SkillChainResolver.GetSkillData(slotData);
            if (skillData == null)
                continue;

            if (request.SkillIds.Length >= request.SkillIds.Capacity ||
                request.SkillAdditionIds.Length >= request.SkillAdditionIds.Capacity)
                break;

            request.SkillIds.Add(skillData.Id);
            request.SkillAdditionIds.Add(slotData.SkillAdditionId);
        }

        if (request.SkillIds.Length == 0)
            return false;

        UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
        request.HasActiveChain = true;
        request.HasPendingCast = true;
        request.CurrentSkillIndex = 0;
        request.HasLockedTarget = true;
        request.LockedTargetPosition = intent.CastTargetPosition;
        return true;
    }

    private void UpdateAnimationFacing(float2 targetPosition, bool hasTarget)
    {
        if (!hasTarget || !EntityManager.HasComponent<LocalTransform>(Entity))
        {
            ClearAnimationFacingDirection();
            return;
        }

        float2 selfPosition = EntityManager.GetComponentData<LocalTransform>(Entity).Position.xy;
        float2 direction = targetPosition - selfPosition;
        if (math.lengthsq(direction) <= 0.0001f)
        {
            ClearAnimationFacingDirection();
            return;
        }

        SetAnimationFacingDirection(direction);
    }
}
