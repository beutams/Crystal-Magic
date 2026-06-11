using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitSkillSystem))]
[UpdateAfter(typeof(UnitControlSystem))]
[UpdateBefore(typeof(UnitStateMachineSystem))]
partial class UnitStateTransitionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (smComp, entity) in SystemAPI.Query<UnitStateMachineComponent>().WithEntityAccess())
        {
            if (smComp.CurrentState == null)
                continue;

            var transitions = smComp.CurrentState.transitions;
            if (transitions == null || transitions.Count == 0)
                continue;

            foreach (var kvp in transitions)
            {
                Comparator comparator = kvp.Value;
                AUnitState target = kvp.Key;

                if (comparator.conditions == null || comparator.GetResult())
                {
                    DoTransition(smComp, target);
                    break;
                }
            }
        }
    }

    private static void DoTransition(UnitStateMachineComponent sm, AUnitState next)
    {
        sm.CurrentState.OnExit();
        sm.PreviousState = sm.CurrentState;
        sm.PreviousStateName = sm.CurrentStateName;
        sm.CurrentState = next;
        sm.CurrentStateName = next.GetType().Name;
        sm.StateTime = 0f;
        sm.CurrentState.OnEnter();
    }
}
