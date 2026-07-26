using System;

public static class UnitStateMachineUtility
{
    public static bool TryForceState(UnitStateMachineComponent stateMachine, string stateType)
    {
        if (stateMachine?.StateInstances == null ||
            string.IsNullOrWhiteSpace(stateType) ||
            !stateMachine.StateInstances.TryGetValue(stateType, out AUnitState next))
        {
            return false;
        }

        if (ReferenceEquals(stateMachine.CurrentState, next))
            return true;

        stateMachine.CurrentState?.OnExit();
        stateMachine.PreviousState = stateMachine.CurrentState;
        stateMachine.PreviousStateName = stateMachine.CurrentStateName;
        stateMachine.CurrentState = next;
        stateMachine.CurrentStateName = next.GetType().Name;
        stateMachine.StateTime = 0f;
        next.OnEnter();
        return true;
    }
}
