using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

[FactoryKey("PublishGameEvent", 12, "Publish Game Event")]
public sealed class PublishGameEventStateScriptNode : StateScriptActionNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly PublishGameEventStateScriptNodeData _data;
    private readonly StateScriptOutputPort _output;
    private string _eventName;
    private Func<UnitValue> _payloadGetter;

    public PublishGameEventStateScriptNode(PublishGameEventStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
        AddInput("In", Publish);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        _eventName = (_data.EventName ?? string.Empty).Trim();
        _payloadGetter = null;
        if (string.IsNullOrWhiteSpace(_eventName))
        {
            error = "PublishGameEvent event name is empty.";
            return false;
        }

        _data.Payload ??= PublishGameEventStateScriptNodeData.CreateDefaultPayloadExpression();
        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Payload,
                Runtime.Sources,
                out _,
                out Func<UnitValue> payloadGetter,
                out error))
        {
            return false;
        }

        _payloadGetter = payloadGetter;
        error = string.Empty;
        return true;
    }

    private void Publish()
    {
        if (_payloadGetter == null || !EventComponent.TryGetInstance(out EventComponent eventComponent))
            return;

        eventComponent.Publish(new CommonGameEvent(
            _eventName,
            new GameplayEventPayload(Runtime.Entity, _payloadGetter())));
        _output.Pulse();
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
