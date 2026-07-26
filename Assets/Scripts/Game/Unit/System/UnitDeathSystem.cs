using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class UnitDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((RefRW<UnitDeathComponent> deathRef,
                  RefRO<UnitVitalityComponent> vitalityRef,
                  UnitStateMachineComponent stateMachine,
                  Entity entity) in
                 SystemAPI.Query<
                         RefRW<UnitDeathComponent>,
                         RefRO<UnitVitalityComponent>,
                         UnitStateMachineComponent>()
                     .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                     .WithEntityAccess())
        {
            if (vitalityRef.ValueRO.CurrentHealth > 0f ||
                EntityManager.IsComponentEnabled<UnitDeathComponent>(entity))
            {
                continue;
            }

            deathRef.ValueRW = new UnitDeathComponent
            {
                Phase = UnitDeathPhase.PlayingAnimation,
                ElapsedSeconds = 0f,
            };
            EntityManager.SetComponentEnabled<UnitDeathComponent>(entity, true);

            if (!UnitStateMachineUtility.TryForceState(stateMachine, "DeathState"))
                UnityEngine.Debug.LogError($"[UnitDeathSystem] {entity} has no DeathState.");
        }
    }
}
