using CrystalMagic.Game.Data;
using System;
using Unity.Entities;

[FactoryKey("RequestInteraction", 14, "Request Interaction")]
public sealed class RequestInteractionActionNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly RequestInteractionActionNodeData _data;
    private readonly StateScriptOutputPort _output;
    private InteractionRequestSourceGet _interactionGetter;
    private Func<UnitValue> _targetGetter;

    public RequestInteractionActionNode(RequestInteractionActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", Request);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _interactionGetter = null;
        _targetGetter = null;
        _data.Interaction ??= RequestInteractionActionNodeData.CreateDefaultInteraction();
        _data.Interaction.EnsureValid();

        if (_data.Interaction.Source == InteractionRequestSource.Getter)
        {
            if (!Runtime.Sources.TryGetInteractionDefinition(_data.Interaction.GetterKey, out _interactionGetter))
            {
                error = $"RequestInteraction getter is not available: {_data.Interaction.GetterKey}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (!_data.Interaction.FixedData.IsValid)
        {
            error = "RequestInteraction fixed data requires a Kind.";
            return false;
        }

        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Interaction.Target,
                Runtime.Sources,
                out UnitValueCategory category,
                out Func<UnitValue> targetGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Entity)
        {
            error = $"RequestInteraction target requires Entity, but received {category}.";
            return false;
        }

        _targetGetter = targetGetter;
        error = string.Empty;
        return true;
    }

    private void Request()
    {
        if (!TryGetInteraction(out InteractionRequestSnapshot interaction) ||
            !GameInteractionRequestUtility.TrySubmit(Runtime.EntityManager, Runtime.Entity, interaction))
        return;

        _output.Pulse();
    }

    private bool TryGetInteraction(out InteractionRequestSnapshot interaction)
    {
        if (_data.Interaction.Source == InteractionRequestSource.Getter)
        {
            if (_interactionGetter != null && _interactionGetter.TryGet(out interaction))
                return true;

            interaction = default;
            return false;
        }

        UnitValue targetValue = _targetGetter == null ? UnitValue.None : _targetGetter();
        if (targetValue.Category != UnitValueCategory.Entity)
        {
            interaction = default;
            return false;
        }

        interaction = new InteractionRequestSnapshot
        {
            Target = targetValue.Entity,
            Data = _data.Interaction.FixedData,
        };
        return interaction.IsValid;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
