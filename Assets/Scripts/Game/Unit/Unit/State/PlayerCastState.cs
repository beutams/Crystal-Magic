using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;

[FactoryKey("PlayerCastState")]
public class PlayerCastState : AUnitState
{
    private readonly System.Collections.Generic.List<SkillChainSlotData> _skillSlots = new();

    public override void OnEnter()
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity) ||
            !EntityManager.HasComponent<UnitIntentComponent>(Entity))
            return;

        SkillCData skillConfig = SaveDataComponent.Instance?.GetSkillData();
        RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();

        if (!SkillChainResolver.TryBuildSelectedChain(skillConfig, runtimeSkillData, _skillSlots, out int chainIndex))
            return;

        Unity.Collections.FixedList64Bytes<int> skillIds = default;
        Unity.Collections.FixedList64Bytes<int> skillAdditionIds = default;

        for (int i = 0; i < _skillSlots.Count; i++)
        {
            SkillChainSlotData slotData = _skillSlots[i];
            SkillData skillData = SkillChainResolver.GetSkillData(slotData);
            if (skillData == null)
                continue;

            if (skillIds.Length >= skillIds.Capacity ||
                skillAdditionIds.Length >= skillAdditionIds.Capacity)
                break;

            skillIds.Add(skillData.Id);
            skillAdditionIds.Add(slotData?.SkillAdditionId ?? -1);
        }

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        SkillExecutionUtility.ResetCastState(ref cast);
        if (skillIds.Length == 0)
        {
            EntityManager.SetComponentData(Entity, cast);
            return;
        }

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(Entity);
        if (SkillExecutionUtility.TryBeginCast(
                EntityManager,
                Entity,
                ref cast,
                skillIds,
                skillAdditionIds,
                chainIndex,
                hasLockedTarget: true,
                lockedTargetPosition: intent.CastTargetPosition))
        {
            EventComponent.Instance.Publish(new SkillCastLockChangedEvent(true));
        }

        EntityManager.SetComponentData(Entity, cast);
    }

    public override void OnUpdate(float deltaTime)
    {
        if (!EntityManager.HasComponent<UnitCastComponent>(Entity))
            return;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
        SkillAdvanceResult result = SkillAdvanceResult.None;

        if (cast.IsCasting)
            result = SkillExecutionUtility.AdvanceCurrentSkill(EntityManager, Entity, deltaTime, ref cast);

        switch (result)
        {
            case SkillAdvanceResult.Completed:
            {
                int nextSkillIndex = cast.CurrentSkillIndex + 1;
                if (nextSkillIndex < cast.SkillIds.Length)
                {
                    if (!SkillExecutionUtility.TryStartSkillAtIndex(EntityManager, Entity, ref cast, nextSkillIndex, out _))
                        SkillExecutionUtility.ResetCastState(ref cast);
                }
                else
                {
                    SkillExecutionUtility.ResetCastState(ref cast);
                }

                break;
            }

            case SkillAdvanceResult.Interrupted:
            case SkillAdvanceResult.Failed:
                SkillExecutionUtility.ResetCastState(ref cast);
                break;
        }

        SkillExecutionUtility.ApplyMovement(EntityManager, Entity, cast);
        EntityManager.SetComponentData(Entity, cast);
    }

    public override void OnExit()
    {
        if (EntityManager.HasComponent<UnitCastComponent>(Entity))
        {
            UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(Entity);
            SkillExecutionUtility.ResetCastState(ref cast);
            SkillExecutionUtility.ApplyMovement(EntityManager, Entity, cast);
            EntityManager.SetComponentData(Entity, cast);
        }

        EventComponent.Instance.Publish(new SkillCastLockChangedEvent(false));
    }
}
