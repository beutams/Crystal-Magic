using Unity.Entities;

[RunInGameWorld(GameWorldKind.Dungeon)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(SkillReleaseSystem))]
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
