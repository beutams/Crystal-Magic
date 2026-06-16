using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(NPCInteractPromptSystem))]
partial struct NPCInteractInputSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NPCInteractionState>();
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<NPCInteractionRequest>())
            return;

        RefRW<NPCInteractionRequest> request = SystemAPI.GetSingletonRW<NPCInteractionRequest>();
        request.ValueRW.Target = Entity.Null;
        request.ValueRW.HasRequest = 0;

        bool wantToInteract = false;
        foreach ((RefRO<PlayerTag> _, RefRO<UnitIntentComponent> intentRef, Entity entity) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<UnitIntentComponent>>().WithEntityAccess())
        {
            if (UnitControlUtility.IsInControlledState(state.EntityManager, entity))
                break;

            wantToInteract = intentRef.ValueRO.WantToInteract;
            break;
        }

        if (!wantToInteract)
            return;

        Entity target = SystemAPI.GetSingleton<NPCInteractionState>().CurrentTarget;
        if (target == Entity.Null)
            return;

        request.ValueRW.Target = target;
        request.ValueRW.HasRequest = 1;
    }
}
