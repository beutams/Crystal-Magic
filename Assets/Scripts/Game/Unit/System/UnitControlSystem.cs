using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(EffectExecutionSystem))]
[UpdateBefore(typeof(UnitMoveSystem))]
partial struct UnitControlSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;

        foreach (var (_, entity) in SystemAPI.Query<RefRO<UnitControlRuntimeComponent>>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            UnitControlUtility.TickAndRefresh(entityManager, entity, deltaTime);
        }
    }
}
