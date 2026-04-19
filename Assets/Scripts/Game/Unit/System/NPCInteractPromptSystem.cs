using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(UnitMoveSystem))]
partial struct NPCInteractPromptSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();

        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new NPCInteractionState
        {
            CurrentTarget = Entity.Null,
        });
        state.EntityManager.AddComponentData(singletonEntity, new NPCInteractionRequest
        {
            Target = Entity.Null,
            HasRequest = 0,
        });
    }

    public void OnUpdate(ref SystemState state)
    {
        float3 playerPosition = float3.zero;
        bool hasPlayer = false;

        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> transform) in
            SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            playerPosition = transform.ValueRO.Position;
            hasPlayer = true;
            break;
        }

        Entity nearestNpc = Entity.Null;
        float nearestDistanceSq = float.MaxValue;

        foreach ((RefRO<NPCTag> _, RefRO<NPCInteractable> interactable, RefRO<LocalTransform> transform, Entity entity) in
            SystemAPI.Query<RefRO<NPCTag>, RefRO<NPCInteractable>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (hasPlayer)
            {
                float distanceSq = math.distancesq(playerPosition, transform.ValueRO.Position);
                if (distanceSq <= interactable.ValueRO.interactRangeSq && distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestNpc = entity;
                }
            }

            SetPromptVisible(state.EntityManager, interactable.ValueRO, shouldShow: false);
        }

        if (nearestNpc != Entity.Null && state.EntityManager.HasComponent<NPCInteractable>(nearestNpc))
        {
            NPCInteractable interactable = state.EntityManager.GetComponentData<NPCInteractable>(nearestNpc);
            SetPromptVisible(state.EntityManager, interactable, shouldShow: true);
        }

        RefRW<NPCInteractionState> interactionState = SystemAPI.GetSingletonRW<NPCInteractionState>();
        interactionState.ValueRW.CurrentTarget = nearestNpc;
    }

    private static void SetPromptVisible(EntityManager entityManager, NPCInteractable interactable, bool shouldShow)
    {
        if (interactable.interact == Entity.Null)
            return;

        if (!entityManager.HasComponent<LocalTransform>(interactable.interact))
            return;

        LocalTransform interactTransform = entityManager.GetComponentData<LocalTransform>(interactable.interact);
        interactTransform.Scale = shouldShow ? interactable.promptVisibleScale : 0f;
        entityManager.SetComponentData(interactable.interact, interactTransform);
    }
}
