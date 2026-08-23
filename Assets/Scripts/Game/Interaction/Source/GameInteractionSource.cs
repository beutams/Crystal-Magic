using System;
using Unity.Entities;

public sealed class GameInteractionSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();

    public override Type ComponentType => typeof(InteractionCandidateComponent);
    public override bool IsGlobal => true;

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("game.interaction.hasCandidate", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("game.interaction.candidateKind", ComponentType, UnitValueCategory.Number, s_noParameters);
        schema.AddGet("game.interaction.candidateTarget", ComponentType, UnitValueCategory.Entity, s_noParameters);
        schema.AddGet("world.interaction.isInteracting", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddInteractionGet("game.interaction.candidate", ComponentType);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<InteractionCandidateComponent>());
        table.AddGet(new UnitSourceGet(
            "game.interaction.hasCandidate",
            UnitValueCategory.Bool,
            s_noParameters,
            _ => UnitValue.FromBool(TryGetCandidate(query, out InteractionCandidateComponent candidate) &&
                                     candidate.IsInteracting == 0 && candidate.Target != Entity.Null && candidate.Data.IsValid)));
        table.AddGet(new UnitSourceGet(
            "game.interaction.candidateKind",
            UnitValueCategory.Number,
            s_noParameters,
            _ => UnitValue.FromInt(TryGetCandidate(query, out InteractionCandidateComponent candidate) && candidate.IsInteracting == 0
                ? (int)candidate.Data.Kind
                : (int)InteractionKind.None)));
        table.AddGet(new UnitSourceGet(
            "game.interaction.candidateTarget",
            UnitValueCategory.Entity,
            s_noParameters,
            _ => UnitValue.FromEntity(TryGetCandidate(query, out InteractionCandidateComponent candidate) && candidate.IsInteracting == 0
                ? candidate.Target
                : Entity.Null)));
        table.AddGet(new UnitSourceGet(
            "world.interaction.isInteracting",
            UnitValueCategory.Bool,
            s_noParameters,
            _ => UnitValue.FromBool(TryGetCandidate(query, out InteractionCandidateComponent candidate) && candidate.IsInteracting != 0)));
        table.AddInteractionGet(new InteractionRequestSourceGet(
            "game.interaction.candidate",
            (out InteractionRequestSnapshot request) =>
            {
                request = default;
                if (!TryGetCandidate(query, out InteractionCandidateComponent candidate) || candidate.IsInteracting != 0)
                    return false;

                request = new InteractionRequestSnapshot
                {
                    Target = candidate.Target,
                    Data = candidate.Data,
                };
                return request.IsValid;
            }));
    }

    private static bool TryGetCandidate(EntityQuery query, out InteractionCandidateComponent candidate)
    {
        if (query.IsEmptyIgnoreFilter)
        {
            candidate = default;
            return false;
        }

        candidate = query.GetSingleton<InteractionCandidateComponent>();
        return true;
    }
}
