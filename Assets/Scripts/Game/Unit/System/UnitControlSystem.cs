using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(PlayerInputSystem))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateBefore(typeof(UnitSkillSystem))]
partial struct UnitControlSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;

        foreach (var (_, entity) in SystemAPI.Query<RefRO<UnitControlRuntimeComponent>>().WithEntityAccess())
            UnitControlUtility.TickAndRefresh(entityManager, entity, deltaTime);
    }
}
