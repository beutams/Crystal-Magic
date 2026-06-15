using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(PlayerInputSystem))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial struct UnitControlSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;

        foreach (var (_, entity) in SystemAPI.Query<RefRO<UnitControlStateComponent>>().WithEntityAccess())
        {
            UnitControlUtility.TickAndRefresh(entityManager, entity, deltaTime);

            UnitControlStateComponent refreshedState = entityManager.GetComponentData<UnitControlStateComponent>(entity);
            if (refreshedState.HasControl == 0)
                continue;

            if (entityManager.HasComponent<UnitIntentComponent>(entity))
            {
                UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
                intent.ClearFrameIntent();
                entityManager.SetComponentData(entity, intent);
            }
        }
    }
}
