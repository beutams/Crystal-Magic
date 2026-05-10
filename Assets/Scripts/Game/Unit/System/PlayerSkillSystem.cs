using CrystalMagic.Core;
using Unity.Entities;

[UpdateAfter(typeof(UnitStateMachineSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class PlayerSkillSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (playerSkillRef, castRef, entity) in SystemAPI.Query<RefRW<PlayerSkillComponent>, RefRW<UnitCastComponent>>().WithAll<PlayerTag>().WithEntityAccess())
        {
            PlayerSkillComponent playerSkill = playerSkillRef.ValueRW;
            if (!playerSkill.HasPendingCast || castRef.ValueRO.IsCasting)
                continue;

            UnitCastComponent cast = castRef.ValueRW;
            if (SkillExecutionUtility.TryBeginCast(
                    EntityManager,
                    entity,
                    ref cast,
                    playerSkill.SkillIds,
                    playerSkill.SkillEffectIds,
                    playerSkill.ChainIndex,
                    playerSkill.HasLockedTarget,
                    playerSkill.LockedTargetPosition))
            {
                EventComponent.Instance.Publish(new SkillCastLockChangedEvent(true));
            }

            playerSkill.Clear();
            playerSkillRef.ValueRW = playerSkill;
            castRef.ValueRW = cast;
        }
    }
}
