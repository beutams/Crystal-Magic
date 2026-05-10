using CrystalMagic.Core;
using Unity.Entities;

[UpdateAfter(typeof(PlayerSkillSystem))]
[UpdateAfter(typeof(UnitSkillSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class SkillExecutionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (castRef, entity) in SystemAPI.Query<RefRW<UnitCastComponent>>().WithEntityAccess())
        {
            UnitCastComponent cast = castRef.ValueRW;
            bool wasCasting = cast.IsCasting;

            if (cast.IsCasting)
                SkillExecutionUtility.AdvanceCast(EntityManager, entity, deltaTime, ref cast);

            SkillExecutionUtility.ApplyMovement(EntityManager, entity, cast);

            if (wasCasting && !cast.IsCasting && EntityManager.HasComponent<PlayerTag>(entity))
                EventComponent.Instance.Publish(new SkillCastLockChangedEvent(false));

            castRef.ValueRW = cast;
        }
    }
}
