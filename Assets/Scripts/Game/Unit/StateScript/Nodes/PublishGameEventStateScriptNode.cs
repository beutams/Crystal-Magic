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
    private Func<UnitValue> _referenceGetter;

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
        _referenceGetter = null;
        if (string.IsNullOrWhiteSpace(_eventName))
        {
            error = "PublishGameEvent event name is empty.";
            return false;
        }

        _data.Reference ??= PublishGameEventStateScriptNodeData.CreateDefaultReferenceExpression();
        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Reference,
                Runtime.Sources,
                out _,
                out Func<UnitValue> referenceGetter,
                out error))
        {
            return false;
        }

        _referenceGetter = referenceGetter;
        error = string.Empty;
        return true;
    }

    private void Publish()
    {
        if (_referenceGetter == null || !EventComponent.TryGetInstance(out EventComponent eventComponent))
            return;

        eventComponent.Publish(new CommonGameEvent(
            _eventName,
            new GameplayEventReference(Runtime.Entity, _referenceGetter())));
        _output.Pulse();
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
