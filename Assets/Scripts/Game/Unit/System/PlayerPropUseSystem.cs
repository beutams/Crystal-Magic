using CrystalMagic.Game;
using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(DungeonExitSystem))]
partial struct PlayerPropUseSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        foreach ((RefRO<PlayerTag> _, RefRO<UnitIntentComponent> intentRef, Entity entity) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<UnitIntentComponent>>().WithEntityAccess())
        {
            if (UnitControlUtility.IsInControlledState(entityManager, entity))
                continue;

            UnitIntentComponent intent = intentRef.ValueRO;
            if (!intent.WantToUseProp || intent.RequestedPropShortcutIndex < 0)
                continue;

            if (!PropUseUtility.TryBuildContext(entityManager, entity, out PropUseRequestContext context, out _))
                continue;

            PropUseUtility.TryUseShortcutSlot(intent.RequestedPropShortcutIndex, context, out _);
        }
    }
}
