using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitStateMachineBuildSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class UnitStateMachineSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (UnitStateMachineComponent sm in SystemAPI.Query<UnitStateMachineComponent>())
        {
            if (sm.CurrentState == null)
                continue;

            sm.StateTime += dt;
            sm.CurrentState.OnUpdate(dt);
        }
    }
}
