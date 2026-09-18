using Unity.Entities;

[UnitSourceProvider(typeof(InteractionCandidateComponent), isGlobal: true)]
public static class GameInteractionSource
{
    [UnitSourceGet(0, "game.interaction.hasCandidate", UnitValueCategory.Bool)]
    [UnitSourceGet(1, "game.interaction.candidateKind", UnitValueCategory.Number)]
    [UnitSourceGet(2, "game.interaction.candidateTarget", UnitValueCategory.Entity)]
    [UnitSourceGet(3, "world.interaction.isInteracting", UnitValueCategory.Bool)]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        bool hasCandidate = TryGetCandidate(entityManager, out InteractionCandidateComponent candidate);
        result = operation switch
        {
            0 => UnitSourceValue.FromBool(hasCandidate && candidate.IsInteracting == 0 &&
                                          candidate.Target != Entity.Null && candidate.Data.IsValid),
            1 => UnitSourceValue.FromInt(hasCandidate && candidate.IsInteracting == 0
                ? (int)candidate.Data.Kind
                : (int)InteractionKind.None),
            2 => UnitSourceValue.FromEntity(hasCandidate && candidate.IsInteracting == 0
                ? candidate.Target
                : Entity.Null),
            3 => UnitSourceValue.FromBool(hasCandidate && candidate.IsInteracting != 0),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    public static bool TryGetInteraction(EntityManager entityManager, out InteractionRequestSnapshot request)
    {
        request = default;
        if (!TryGetCandidate(entityManager, out InteractionCandidateComponent candidate) || candidate.IsInteracting != 0)
            return false;

        request = new InteractionRequestSnapshot
        {
            Target = candidate.Target,
            Data = candidate.Data,
        };
        return request.IsValid;
    }

    private static bool TryGetCandidate(EntityManager entityManager, out InteractionCandidateComponent candidate)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<InteractionCandidateComponent>());
        if (query.IsEmptyIgnoreFilter)
        {
            candidate = default;
            return false;
        }

        candidate = query.GetSingleton<InteractionCandidateComponent>();
        return true;
    }
}
