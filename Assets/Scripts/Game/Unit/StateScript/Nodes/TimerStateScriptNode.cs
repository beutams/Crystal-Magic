using CrystalMagic.Game.Data;
using UnityEngine;

[FactoryKey("Timer", 20, "Timer")]
public sealed class TimerStateScriptNode : StateScriptStateNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

    private readonly TimerStateScriptNodeData _data;
    private float _elapsedSeconds;
    private float _durationSeconds;
    private System.Func<UnitValue> _durationGetter;

    public TimerStateScriptNode(TimerStateScriptNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        _data = data;
    }

    protected override bool OnBind(out string error)
    {
        _durationGetter = null;
        _data.Duration ??= TimerStateScriptNodeData.CreateDefaultDurationExpression();
        if (!s_expressionFactory.TryBuildValueExpression(
                _data.Duration,
                Runtime.Sources,
                out UnitValueCategory category,
                out System.Func<UnitValue> durationGetter,
                out error))
        {
            return false;
        }

        if (category != UnitValueCategory.Number)
        {
            error = $"Timer Duration requires Number, but received {category}.";
            return false;
        }

        _durationGetter = durationGetter;
        error = string.Empty;
        return true;
    }

    protected override void OnActivate()
    {
        _elapsedSeconds = 0f;
        _durationSeconds = ResolveDurationSeconds();
    }

    protected override void OnUpdate()
    {
        _elapsedSeconds += Runtime.DeltaTime;
        if (_elapsedSeconds >= _durationSeconds)
            Complete();
    }

    private float ResolveDurationSeconds()
    {
        if (_durationGetter == null ||
            !_durationGetter().TryGetNumber(out float durationSeconds) ||
            float.IsNaN(durationSeconds) ||
            float.IsInfinity(durationSeconds))
        {
            return 0f;
        }

        return Mathf.Max(0f, durationSeconds);
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}
