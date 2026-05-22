using CrystalMagic.Core;
using Unity.Entities;

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
    }

    public override void OnUpdate(float deltaTime)
    {
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
    }
}
