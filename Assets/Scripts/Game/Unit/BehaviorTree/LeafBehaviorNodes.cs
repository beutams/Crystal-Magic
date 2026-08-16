using System;
using CrystalMagic.Game.Data;
using UnityEngine;

[FactoryKey(BehaviorNodeTypes.Set, 11, "Set")]
public sealed class SetBehaviorNode : ActionBehaviorNode
{
    private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();
    private readonly SetBehaviorNodeData _data;
    private UnitSourceSet _set;
    private string _key;
    private Func<UnitValue>[] _inputGetters;
    private UnitValue[] _inputValues;

    public SetBehaviorNode(SetBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override bool OnBind(UnitSourceAccessTable sources, out string error)
    {
        _set = null;
        _key = string.Empty;
        _inputGetters = null;
        _inputValues = null;
        if (_data == null || string.IsNullOrWhiteSpace(_data.SetKey))
        {
            error = "Set key is empty.";
            return false;
        }

        if (!sources.TryGetDefinition(_data.SetKey, out UnitSourceSet set))
        {
            error = $"Set '{_data.SetKey}' is unavailable on this unit.";
            return false;
        }

        if (_data.Inputs == null || _data.Inputs.Count != set.Parameters.Count)
        {
            error = $"Set '{_data.SetKey}' requires {set.Parameters.Count} input(s).";
            return false;
        }

        if (set.RequiresKey && string.IsNullOrWhiteSpace(_data.Key))
        {
            error = $"Set '{_data.SetKey}' requires a configured key.";
            return false;
        }

        Func<UnitValue>[] inputGetters = new Func<UnitValue>[set.Parameters.Count];
        for (int i = 0; i < set.Parameters.Count; i++)
        {
            if (!s_expressionFactory.TryBuildValueExpression(
                    _data.Inputs[i],
                    sources,
                    out UnitValueCategory category,
                    out Func<UnitValue> getter,
                    out error))
            {
                return false;
            }

            if (!set.Parameters[i].Accepts(category))
            {
                error = $"Set '{_data.SetKey}' input '{set.Parameters[i].Name}' requires {set.Parameters[i].Category}, but received {category}.";
                return false;
            }

            inputGetters[i] = getter;
        }

        _set = set;
        _key = _data.Key ?? string.Empty;
        _inputGetters = inputGetters;
        _inputValues = new UnitValue[inputGetters.Length];
        error = string.Empty;
        return true;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorContext context)
    {
        if (_set == null || _inputGetters == null)
            return BehaviorNodeStatus.Failure;

        for (int i = 0; i < _inputGetters.Length; i++)
        {
            Func<UnitValue> getter = _inputGetters[i];
            if (getter == null)
                return BehaviorNodeStatus.Failure;

            _inputValues[i] = getter();
        }

        bool didSet = _set.RequiresKey
            ? _set.TrySet(_key, _inputValues[0])
            : _set.TrySet(_inputValues);
        return didSet
            ? BehaviorNodeStatus.Success
            : BehaviorNodeStatus.Failure;
    }

    private static ComparatorFactory CreateExpressionFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }
}

[FactoryKey(BehaviorNodeTypes.Wait, 12, "Wait")]
public sealed class WaitBehaviorNode : ActionBehaviorNode
{
    private readonly WaitBehaviorNodeData _data;
    private float _elapsedSeconds;

    public WaitBehaviorNode(WaitBehaviorNodeData data)
        : base(data)
    {
        _data = data;
    }

    protected override BehaviorNodeStatus OnTick(BehaviorContext context)
    {
        float duration = Mathf.Max(0f, _data?.DurationSeconds ?? 0f);
        _elapsedSeconds += Mathf.Max(0f, context?.DeltaTime ?? 0f);
        if (_elapsedSeconds < duration)
            return BehaviorNodeStatus.Running;

        _elapsedSeconds = 0f;
        return BehaviorNodeStatus.Success;
    }

    public override void Reset()
    {
        _elapsedSeconds = 0f;
        base.Reset();
    }
}
