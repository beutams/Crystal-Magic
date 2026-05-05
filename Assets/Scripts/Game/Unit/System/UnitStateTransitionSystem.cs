using Unity.Entities;

[UpdateAfter(typeof(UnitStateMachineSystem))]
partial class UnitStateTransitionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (smComp, entity) in SystemAPI.Query<UnitStateMachineComponent>().WithEntityAccess())
        {
            if (smComp.CurrentState == null)
                continue;

            if (smComp.CurrentState is CastState &&
                EntityManager.HasComponent<UnitCastComponent>(entity) &&
                EntityManager.GetComponentData<UnitCastComponent>(entity).IsCasting)
            {
                continue;
            }

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

[UpdateAfter(typeof(UnitStateMachineSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial struct UnitHitStunSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;

        foreach (var (hitStun, entity) in SystemAPI.Query<RefRW<UnitHitStunComponent>>().WithEntityAccess())
        {
            if (entityManager.HasComponent<UnitIntentComponent>(entity))
            {
                UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
                intent.MoveDirection = Unity.Mathematics.float2.zero;
                intent.WantToCast = false;
                intent.HasCastTarget = false;
                intent.CastTargetPosition = Unity.Mathematics.float2.zero;
                entityManager.SetComponentData(entity, intent);
            }

            if (entityManager.HasComponent<UnitMoveComponent>(entity))
            {
                UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
                move.AccelInput = Unity.Mathematics.float2.zero;
                move.Velocity = Unity.Mathematics.float2.zero;
                entityManager.SetComponentData(entity, move);
            }

            if (entityManager.HasComponent<UnitCastComponent>(entity))
            {
                UnitCastComponent cast = entityManager.GetComponentData<UnitCastComponent>(entity);
                cast.ForceInterrupt = true;
                entityManager.SetComponentData(entity, cast);
            }

            float remainingSeconds = hitStun.ValueRO.RemainingSeconds - deltaTime;
            if (remainingSeconds <= 0f)
            {
                entityManager.RemoveComponent<UnitHitStunComponent>(entity);
                continue;
            }

            hitStun.ValueRW.RemainingSeconds = remainingSeconds;
        }
    }
}

public struct UnitHitStunComponent : IComponentData
{
    public float RemainingSeconds;
}
