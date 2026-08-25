using Unity.Entities;

public static class GameInteractionRequestUtility
{
    public static bool TrySubmit(
        EntityManager entityManager,
        Entity actor,
        in InteractionRequestSnapshot interaction)
    {
        EntityQuery candidateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<InteractionCandidateComponent>());
        EntityQuery requestQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameInteractionRequest>());
        if (candidateQuery.IsEmptyIgnoreFilter || requestQuery.IsEmptyIgnoreFilter || !interaction.IsValid)
            return false;

        InteractionCandidateComponent candidate = candidateQuery.GetSingleton<InteractionCandidateComponent>();
        if (candidate.IsInteracting != 0)
            return false;

        Entity requestEntity = requestQuery.GetSingletonEntity();
        GameInteractionRequest request = entityManager.GetComponentData<GameInteractionRequest>(requestEntity);
        if (request.HasRequest != 0)
            return false;

        request = new GameInteractionRequest
        {
            Actor = actor,
            Target = interaction.Target,
            Data = interaction.Data,
            HasRequest = 1,
        };
        entityManager.SetComponentData(requestEntity, request);
        return true;
    }
}
