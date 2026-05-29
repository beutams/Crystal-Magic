using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
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
        if (PlayerSkillAnalysisSystem.TryQueueSelectedChainRequest(EntityManager, Entity, ref request))
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
