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
        in InteractionCandidateComponent candidate,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromBool(candidate.IsInteracting == 0 &&
                                          candidate.Target != Entity.Null && candidate.Data.IsValid),
            1 => UnitSourceValue.FromInt(candidate.IsInteracting == 0
                ? (int)candidate.Data.Kind
                : (int)InteractionKind.None),
            2 => UnitSourceValue.FromEntity(candidate.IsInteracting == 0
                ? candidate.Target
                : Entity.Null),
            3 => UnitSourceValue.FromBool(candidate.IsInteracting != 0),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    public static bool TryGetInteraction(
        in InteractionCandidateComponent candidate,
        out InteractionRequestSnapshot request)
    {
        request = default;
        if (candidate.IsInteracting != 0)
            return false;

        request = new InteractionRequestSnapshot
        {
            Target = candidate.Target,
            Data = candidate.Data,
        };
        return request.IsValid;
    }
}
