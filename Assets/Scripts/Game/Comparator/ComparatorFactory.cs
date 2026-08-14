using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public interface IValueOperation
{
    IReadOnlyList<ComparatorParameterDefinition> Parameters { get; }
    UnitValueCategory ResultCategory { get; }
    bool TryEvaluate(UnitValue[] values, out UnitValue result);
}

[FactoryKey("Add")]
[EditorLabel("Add")]
public sealed class AddOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.NumberPair();
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyNumberPair(values, static (left, right) => left + right, out result);
}

[FactoryKey("Subtract")]
[EditorLabel("Subtract")]
public sealed class SubtractOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.NumberPair();
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyNumberPair(values, static (left, right) => left - right, out result);
}

[FactoryKey("Multiply")]
[EditorLabel("Multiply")]
public sealed class MultiplyOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.NumberPair();
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyNumberPair(values, static (left, right) => left * right, out result);
}

[FactoryKey("Divide")]
[EditorLabel("Divide")]
public sealed class DivideOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.NumberPair();
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (!ComparatorFactory.TryGetNumberPair(values, out float left, out float right) || math.abs(right) <= 0.0001f)
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(left / right);
        return true;
    }
}

[FactoryKey("Min")]
[EditorLabel("Minimum")]
public sealed class MinOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.NumberPair();
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyNumberPair(values, math.min, out result);
}

[FactoryKey("Max")]
[EditorLabel("Maximum")]
public sealed class MaxOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.NumberPair();
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyNumberPair(values, math.max, out result);
}

[FactoryKey("Abs")]
[EditorLabel("Absolute")]
public sealed class AbsOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Value", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (values == null || values.Length != 1 || !values[0].TryGetNumber(out float value))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(math.abs(value));
        return true;
    }
}

[FactoryKey("Clamp")]
[EditorLabel("Clamp")]
public sealed class ClampOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Value", UnitValueCategory.Number),
        new("Minimum", UnitValueCategory.Number),
        new("Maximum", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (values == null || values.Length != 3 ||
            !values[0].TryGetNumber(out float value) ||
            !values[1].TryGetNumber(out float minimum) ||
            !values[2].TryGetNumber(out float maximum))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(math.clamp(value, minimum, maximum));
        return true;
    }
}

[FactoryKey("Distance")]
[EditorLabel("Distance")]
public sealed class DistanceOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.Float3Pair("Position A", "Position B");
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyFloat3Pair(values, math.distance, out result);
}

[FactoryKey("DistanceSquared")]
[EditorLabel("Distance squared")]
public sealed class DistanceSquaredOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.Float3Pair("Position A", "Position B");
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyFloat3Pair(values, math.distancesq, out result);
}

[FactoryKey("Length")]
[EditorLabel("Length")]
public sealed class LengthOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Vector", UnitValueCategory.Float3),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (values == null || values.Length != 1 || !values[0].TryGetFloat3(out float3 vector))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(math.length(vector));
        return true;
    }
}

[FactoryKey("LengthSquared")]
[EditorLabel("Length squared")]
public sealed class LengthSquaredOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Vector", UnitValueCategory.Float3),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (values == null || values.Length != 1 || !values[0].TryGetFloat3(out float3 vector))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(math.lengthsq(vector));
        return true;
    }
}

[FactoryKey("Dot")]
[EditorLabel("Dot product")]
public sealed class DotOperation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters = ComparatorFactory.Float3Pair("Vector A", "Vector B");
    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;
    public bool TryEvaluate(UnitValue[] values, out UnitValue result) => ComparatorFactory.TryApplyFloat3Pair(values, math.dot, out result);
}

public class ComparatorFactory
{
    private readonly GeneratedFactory<string, ISource> _sourceFactories = new(StringComparer.Ordinal);
    private readonly GeneratedFactory<string, ICompareType> _compareFactories = new(StringComparer.Ordinal);
    private readonly GeneratedFactory<string, IValueOperation> _operationFactories = new(StringComparer.Ordinal);

    public void RegisterSource(string key, Func<ISource> factory)
    {
        _sourceFactories.Register(key, factory);
    }

    public void RegisterCompareType(string key, Func<ICompareType> factory)
    {
        _compareFactories.Register(key, factory);
    }

    public void RegisterValueOperation(string key, Func<IValueOperation> factory)
    {
        _operationFactories.Register(key, factory);
    }

    public ISource CreateSource(string typeName)
    {
        if (_sourceFactories.TryCreate(typeName, out ISource source))
            return source;

        Debug.LogError($"[ComparatorFactory] Unregistered legacy source: {typeName}");
        return null;
    }

    public ICompareType CreateCompareType(string typeName)
    {
        if (TryCreateCompareType(typeName, out ICompareType compareType))
            return compareType;

        Debug.LogError($"[ComparatorFactory] Unregistered compare type: {typeName}");
        return null;
    }

    public IValueOperation CreateValueOperation(string typeName)
    {
        if (TryCreateValueOperation(typeName, out IValueOperation operation))
            return operation;

        Debug.LogError($"[ComparatorFactory] Unregistered value operation: {typeName}");
        return null;
    }

    public bool TryCreateCompareType(string typeName, out ICompareType compareType)
    {
        return _compareFactories.TryCreate(typeName ?? string.Empty, out compareType);
    }

    public bool TryCreateValueOperation(string typeName, out IValueOperation operation)
    {
        return _operationFactories.TryCreate(typeName ?? string.Empty, out operation);
    }

    public ICollection<string> CompareTypeKeys => _compareFactories.Keys;
    public ICollection<string> ValueOperationKeys => _operationFactories.Keys;

    public bool TryBuildValueExpression(
        ValueExpression expression,
        IComparatorValueResolver resolver,
        out UnitValueCategory category,
        out Func<UnitValue> getter,
        out string error)
    {
        if (!TryBuildValueExpression(expression, resolver, out CompiledValueExpression compiled, out error))
        {
            category = UnitValueCategory.None;
            getter = null;
            return false;
        }

        category = compiled.Category;
        getter = compiled.Getter;
        return true;
    }

    // New entry point. The caller owns the resolver that maps getter keys to cached delegates.
    public Comparator BuildComparator(IReadOnlyList<ConditionConfig> configs, IComparatorValueResolver resolver)
    {
        if (configs == null || configs.Count == 0)
            return new Comparator(Array.Empty<Condition>());

        if (resolver == null)
        {
            Debug.LogError("[ComparatorFactory] A value resolver is required for expression conditions.");
            return new Comparator(Array.Empty<Condition>(), false);
        }

        Condition[] conditions = new Condition[configs.Count];
        for (int i = 0; i < configs.Count; i++)
        {
            if (!TryBuildExpressionCondition(configs[i], resolver, out Condition condition, out string error))
            {
                Debug.LogError($"[ComparatorFactory] Failed to build condition {i}: {error}");
                return new Comparator(Array.Empty<Condition>(), false);
            }

            conditions[i] = condition;
        }

        return new Comparator(conditions);
    }

    // Temporary overload for current modules. It converts their old source/value fields into typed inputs.
    public Comparator BuildComparator(
        List<ConditionConfig> configs,
        Entity entity,
        EntityManager entityManager,
        Entity originEntity = default,
        bool hasOriginEntity = false)
    {
        if (configs == null || configs.Count == 0)
            return new Comparator(Array.Empty<Condition>());

        SourceContext context = new(entity, entityManager, originEntity, hasOriginEntity);
        Condition[] conditions = new Condition[configs.Count];
        for (int i = 0; i < configs.Count; i++)
        {
            ConditionConfig config = configs[i];
            bool hasExpressions = config?.Inputs != null && config.Inputs.Count > 0;
            bool built;
            string error;
            if (hasExpressions)
            {
                built = false;
                error = "Expression conditions require IComparatorValueResolver.";
            }
            else
            {
                built = TryBuildLegacyCondition(config, context, out conditions[i], out error);
            }

            if (!built)
            {
                Debug.LogError($"[ComparatorFactory] Failed to build legacy condition {i}: {error}");
                return new Comparator(Array.Empty<Condition>(), false);
            }
        }

        return new Comparator(conditions);
    }

    public int SourceCount => _sourceFactories.Count;
    public int CompareCount => _compareFactories.Count;
    public int OperationCount => _operationFactories.Count;

    private bool TryBuildExpressionCondition(
        ConditionConfig config,
        IComparatorValueResolver resolver,
        out Condition condition,
        out string error)
    {
        condition = null;
        if (config == null)
        {
            error = "Configuration is null.";
            return false;
        }

        ICompareType compareType = CreateCompareType(config.CompareType);
        if (compareType == null)
        {
            error = $"Compare type '{config.CompareType}' is unavailable.";
            return false;
        }

        if (!TryBuildInputs(config.Inputs, compareType.Parameters, resolver, out Func<UnitValue>[] getters, out error))
            return false;

        condition = new Condition(config.ConditionType, compareType, getters);
        return true;
    }

    private bool TryBuildLegacyCondition(
        ConditionConfig config,
        in SourceContext context,
        out Condition condition,
        out string error)
    {
        condition = null;
        if (config == null)
        {
            error = "Configuration is null.";
            return false;
        }

        ISource source = CreateSource(config.SourceType);
        ICompareType compareType = CreateCompareType(config.CompareType);
        if (source == null || compareType == null)
        {
            error = "Legacy source or compare type is unavailable.";
            return false;
        }

        SourceContext sourceContext = new(
            context.Entity,
            context.EntityManager,
            context.OriginEntity,
            context.HasOriginEntity,
            config.SourceParam,
            context.UnitPrefab,
            context.UnitData,
            context.HasRuntimeEntity);
        source.Init(sourceContext);

        IReadOnlyList<ComparatorParameterDefinition> parameters = compareType.Parameters;
        if (parameters.Count != 1 && parameters.Count != 2)
        {
            error = $"Compare type '{config.CompareType}' cannot be represented by the legacy configuration.";
            return false;
        }

        if (!parameters[0].Accepts(UnitValueCategory.Number))
        {
            error = $"Compare type '{config.CompareType}' does not accept the legacy numeric source.";
            return false;
        }

        Func<UnitValue> sourceGetter = () => UnitValue.FromFloat(source.GetValue());
        if (parameters.Count == 1)
        {
            condition = new Condition(config.ConditionType, compareType, new[] { sourceGetter });
            error = string.Empty;
            return true;
        }

        if (!parameters[1].Accepts(UnitValueCategory.Number))
        {
            error = $"Compare type '{config.CompareType}' does not accept the legacy numeric threshold.";
            return false;
        }

        UnitValue threshold = UnitValue.FromFloat(config.CompareValue);
        condition = new Condition(config.ConditionType, compareType, new Func<UnitValue>[]
        {
            sourceGetter,
            () => threshold,
        });
        error = string.Empty;
        return true;
    }

    private bool TryBuildInputs(
        List<ValueExpression> expressions,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        IComparatorValueResolver resolver,
        out Func<UnitValue>[] getters,
        out string error)
    {
        getters = null;
        if (expressions == null || expressions.Count != parameters.Count)
        {
            error = $"Expected {parameters.Count} input(s), received {expressions?.Count ?? 0}.";
            return false;
        }

        getters = new Func<UnitValue>[parameters.Count];
        for (int i = 0; i < parameters.Count; i++)
        {
            if (!TryBuildValueExpression(expressions[i], resolver, out CompiledValueExpression expression, out error))
                return false;

            if (!parameters[i].Accepts(expression.Category))
            {
                error = $"Input '{parameters[i].Name}' requires {parameters[i].Category}, but received {expression.Category}.";
                return false;
            }

            getters[i] = expression.Getter;
        }

        error = string.Empty;
        return true;
    }

    private bool TryBuildValueExpression(
        ValueExpression expression,
        IComparatorValueResolver resolver,
        out CompiledValueExpression compiled,
        out string error)
    {
        compiled = default;
        if (expression == null)
        {
            error = "Expression is null.";
            return false;
        }

        switch (expression.Kind)
        {
            case ValueExpressionKind.Literal:
                if (expression.Literal.Category == UnitValueCategory.None)
                {
                    error = "Literal has no value type.";
                    return false;
                }

                UnitValue literal = expression.Literal;
                compiled = new CompiledValueExpression(literal.Category, () => literal);
                error = string.Empty;
                return true;

            case ValueExpressionKind.Getter:
                if (string.IsNullOrWhiteSpace(expression.GetterKey))
                {
                    error = "Getter key is empty.";
                    return false;
                }

                if (!resolver.TryGet(expression.GetterKey, out IParameterizedUnitValueGetter getter) || getter == null)
                {
                    error = $"Getter '{expression.GetterKey}' is unavailable.";
                    return false;
                }

                if (!TryBuildInputs(expression.Inputs, getter.Parameters, resolver, out Func<UnitValue>[] getterInputs, out error))
                    return false;

                UnitValue[] getterValues = new UnitValue[getterInputs.Length];
                Func<UnitValue> getterFunction = () =>
                {
                    for (int i = 0; i < getterInputs.Length; i++)
                        getterValues[i] = getterInputs[i]();

                    return getter.TryGet(getterValues, out UnitValue value)
                        ? value
                        : UnitValue.None;
                };

                compiled = new CompiledValueExpression(getter.ReturnType, getterFunction);
                error = string.Empty;
                return true;

            case ValueExpressionKind.Operation:
                return TryBuildOperationExpression(expression, resolver, out compiled, out error);

            default:
                error = $"Unsupported expression kind '{expression.Kind}'.";
                return false;
        }
    }

    private bool TryBuildOperationExpression(
        ValueExpression expression,
        IComparatorValueResolver resolver,
        out CompiledValueExpression compiled,
        out string error)
    {
        compiled = default;
        IValueOperation operation = CreateValueOperation(expression.OperationType);
        if (operation == null)
        {
            error = $"Operation '{expression.OperationType}' is unavailable.";
            return false;
        }

        if (!TryBuildInputs(expression.Inputs, operation.Parameters, resolver, out Func<UnitValue>[] childGetters, out error))
            return false;

        UnitValue[] values = new UnitValue[childGetters.Length];
        Func<UnitValue> operationGetter = () =>
        {
            for (int i = 0; i < childGetters.Length; i++)
                values[i] = childGetters[i]();

            return operation.TryEvaluate(values, out UnitValue result) ? result : UnitValue.None;
        };

        compiled = new CompiledValueExpression(operation.ResultCategory, operationGetter);
        error = string.Empty;
        return true;
    }

    private readonly struct CompiledValueExpression
    {
        public CompiledValueExpression(UnitValueCategory category, Func<UnitValue> getter)
        {
            Category = category;
            Getter = getter;
        }

        public UnitValueCategory Category { get; }
        public Func<UnitValue> Getter { get; }
    }

    internal static ComparatorParameterDefinition[] NumberPair()
    {
        return new[]
        {
            new ComparatorParameterDefinition("Left", UnitValueCategory.Number),
            new ComparatorParameterDefinition("Right", UnitValueCategory.Number),
        };
    }

    internal static ComparatorParameterDefinition[] Float3Pair(string leftName, string rightName)
    {
        return new[]
        {
            new ComparatorParameterDefinition(leftName, UnitValueCategory.Float3),
            new ComparatorParameterDefinition(rightName, UnitValueCategory.Float3),
        };
    }

    internal static bool TryApplyNumberPair(UnitValue[] values, Func<float, float, float> operation, out UnitValue result)
    {
        if (!TryGetNumberPair(values, out float left, out float right))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(operation(left, right));
        return true;
    }

    internal static bool TryGetNumberPair(UnitValue[] values, out float left, out float right)
    {
        if (values != null && values.Length == 2 &&
            values[0].TryGetNumber(out left) && values[1].TryGetNumber(out right))
            return true;

        left = 0f;
        right = 0f;
        return false;
    }

    internal static bool TryApplyFloat3Pair(UnitValue[] values, Func<float3, float3, float> operation, out UnitValue result)
    {
        if (values == null || values.Length != 2 ||
            !values[0].TryGetFloat3(out float3 left) ||
            !values[1].TryGetFloat3(out float3 right))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat(operation(left, right));
        return true;
    }
}
