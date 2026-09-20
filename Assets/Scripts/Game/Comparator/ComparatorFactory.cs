using System;
using System.Collections.Generic;
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

[FactoryKey("Length2")]
[EditorLabel("Length (Float2)")]
public sealed class Length2Operation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Vector", UnitValueCategory.Float2),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Number;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (values == null || values.Length != 1 || !values[0].TryGetFloat2(out float2 vector))
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

[FactoryKey("ScaleFloat2")]
[EditorLabel("Scale Float2")]
public sealed class ScaleFloat2Operation : IValueOperation
{
    private static readonly ComparatorParameterDefinition[] s_parameters =
    {
        new("Vector", UnitValueCategory.Float2),
        new("Scale", UnitValueCategory.Number),
    };

    public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;
    public UnitValueCategory ResultCategory => UnitValueCategory.Float2;

    public bool TryEvaluate(UnitValue[] values, out UnitValue result)
    {
        if (values == null || values.Length != 2 ||
            !values[0].TryGetFloat2(out float2 vector) ||
            !values[1].TryGetNumber(out float scale))
        {
            result = UnitValue.None;
            return false;
        }

        result = UnitValue.FromFloat2(vector * scale);
        return true;
    }
}

public class ComparatorFactory
{
    private readonly GeneratedFactory<string, ICompareType> _compareFactories = new(StringComparer.Ordinal);
    private readonly GeneratedFactory<string, IValueOperation> _operationFactories = new(StringComparer.Ordinal);

    public void RegisterCompareType(string key, Func<ICompareType> factory)
    {
        _compareFactories.Register(key, factory);
    }

    public void RegisterValueOperation(string key, Func<IValueOperation> factory)
    {
        _operationFactories.Register(key, factory);
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
        out CompiledValueExpression compiled,
        out string error)
    {
        compiled = default;
        if (resolver == null)
        {
            error = "A value resolver is required for source expressions.";
            return false;
        }

        ExpressionProgram program = default;
        if (!TryCompileExpression(expression, resolver, ref program, out UnitValueCategory category, out error))
            return false;

        compiled = new CompiledValueExpression(category, in program);
        error = string.Empty;
        return true;
    }

    public Comparator BuildComparator(IReadOnlyList<ConditionConfig> configs, IComparatorValueResolver resolver)
    {
        ExpressionProgram program = default;
        if (configs == null || configs.Count == 0)
            return new Comparator(in program);

        if (resolver == null)
        {
            Debug.LogError("[ComparatorFactory] A value resolver is required for expression conditions.");
            return new Comparator(in program, false);
        }

        for (int i = 0; i < configs.Count; i++)
        {
            if (!TryCompileCondition(configs[i], resolver, ref program, out string error))
            {
                Debug.LogError($"[ComparatorFactory] Failed to build condition {i}: {error}");
                return new Comparator(in program, false);
            }
        }

        return new Comparator(in program);
    }

    public int CompareCount => _compareFactories.Count;
    public int OperationCount => _operationFactories.Count;

    private bool TryCompileCondition(
        ConditionConfig config,
        IComparatorValueResolver resolver,
        ref ExpressionProgram program,
        out string error)
    {
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

        if (!TryGetCompareCode(compareType, out CompareOperationCode compareCode))
        {
            error = $"Compare type '{config.CompareType}' has no unmanaged runtime opcode.";
            return false;
        }

        if (!TryCompileInputs(config.Inputs, compareType.Parameters, resolver, ref program, out error))
            return false;

        if (!TryAppendInstruction(
                ref program,
                new ExpressionInstruction
                {
                    Kind = ExpressionInstructionKind.Compare,
                    InputCount = (byte)compareType.Parameters.Count,
                    Compare = compareCode,
                    ConditionType = config.ConditionType,
                },
                out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryCompileInputs(
        List<ValueExpression> expressions,
        IReadOnlyList<ComparatorParameterDefinition> parameters,
        IComparatorValueResolver resolver,
        ref ExpressionProgram program,
        out string error)
    {
        if (expressions == null || expressions.Count != parameters.Count || parameters.Count > byte.MaxValue)
        {
            error = $"Expected {parameters.Count} input(s), received {expressions?.Count ?? 0}.";
            return false;
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            if (!TryCompileExpression(expressions[i], resolver, ref program, out UnitValueCategory category, out error))
                return false;

            if (!parameters[i].Accepts(category))
            {
                error = $"Input '{parameters[i].Name}' requires {parameters[i].Category}, but received {category}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryCompileExpression(
        ValueExpression expression,
        IComparatorValueResolver resolver,
        ref ExpressionProgram program,
        out UnitValueCategory category,
        out string error)
    {
        category = UnitValueCategory.None;
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

                if (!UnitSourceValue.TryFromUnitValue(expression.Literal, out UnitSourceValue literal) ||
                    !program.TryAddLiteral(in literal, out ushort literalIndex))
                {
                    error = "Expression contains too many literals or an invalid literal value.";
                    return false;
                }

                if (!TryAppendInstruction(
                        ref program,
                        new ExpressionInstruction
                        {
                            Kind = ExpressionInstructionKind.Literal,
                            LiteralIndex = literalIndex,
                        },
                        out error))
                {
                    return false;
                }

                category = expression.Literal.Category;
                error = string.Empty;
                return true;

            case ValueExpressionKind.Getter:
                if (string.IsNullOrWhiteSpace(expression.GetterKey))
                {
                    error = "Getter key is empty.";
                    return false;
                }

                if (!UnitComponentSourceRegistry.TryGetGet(
                        expression.GetterKey,
                        out UnitSourceId sourceId,
                        out UnitSourceGetSchemaEntry schema))
                {
                    return TryCompileConstantGetter(
                        expression,
                        resolver,
                        ref program,
                        out category,
                        out error);
                }

                if (!TryCompileInputs(expression.Inputs, schema.Parameters, resolver, ref program, out error))
                    return false;

                if (!TryAppendInstruction(
                        ref program,
                        new ExpressionInstruction
                        {
                            Kind = ExpressionInstructionKind.Source,
                            InputCount = (byte)schema.Parameters.Count,
                            SourceId = sourceId,
                            SourceTarget = expression.SourceTarget,
                        },
                        out error))
                {
                    return false;
                }

                category = schema.ReturnType;
                error = string.Empty;
                return true;

            case ValueExpressionKind.Operation:
                return TryCompileOperationExpression(expression, resolver, ref program, out category, out error);

            default:
                error = $"Unsupported expression kind '{expression.Kind}'.";
                return false;
        }
    }

    private bool TryCompileOperationExpression(
        ValueExpression expression,
        IComparatorValueResolver resolver,
        ref ExpressionProgram program,
        out UnitValueCategory category,
        out string error)
    {
        category = UnitValueCategory.None;
        IValueOperation operation = CreateValueOperation(expression.OperationType);
        if (operation == null)
        {
            error = $"Operation '{expression.OperationType}' is unavailable.";
            return false;
        }

        if (!TryGetOperationCode(operation, out ValueOperationCode operationCode))
        {
            error = $"Operation '{expression.OperationType}' has no unmanaged runtime opcode.";
            return false;
        }

        if (!TryCompileInputs(expression.Inputs, operation.Parameters, resolver, ref program, out error))
            return false;

        if (!TryAppendInstruction(
                ref program,
                new ExpressionInstruction
                {
                    Kind = ExpressionInstructionKind.Operation,
                    InputCount = (byte)operation.Parameters.Count,
                    Operation = operationCode,
                },
                out error))
        {
            return false;
        }

        category = operation.ResultCategory;
        error = string.Empty;
        return true;
    }

    private static bool TryAppendInstruction(
        ref ExpressionProgram program,
        in ExpressionInstruction instruction,
        out string error)
    {
        if (!program.TryAddInstruction(in instruction))
        {
            error = "Expression contains too many instructions.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetOperationCode(IValueOperation operation, out ValueOperationCode code)
    {
        switch (operation)
        {
            case AddOperation: code = ValueOperationCode.Add; return true;
            case SubtractOperation: code = ValueOperationCode.Subtract; return true;
            case MultiplyOperation: code = ValueOperationCode.Multiply; return true;
            case DivideOperation: code = ValueOperationCode.Divide; return true;
            case MinOperation: code = ValueOperationCode.Min; return true;
            case MaxOperation: code = ValueOperationCode.Max; return true;
            case AbsOperation: code = ValueOperationCode.Abs; return true;
            case ClampOperation: code = ValueOperationCode.Clamp; return true;
            case DistanceOperation: code = ValueOperationCode.Distance; return true;
            case DistanceSquaredOperation: code = ValueOperationCode.DistanceSquared; return true;
            case LengthOperation: code = ValueOperationCode.Length; return true;
            case Length2Operation: code = ValueOperationCode.Length2; return true;
            case LengthSquaredOperation: code = ValueOperationCode.LengthSquared; return true;
            case DotOperation: code = ValueOperationCode.Dot; return true;
            case ScaleFloat2Operation: code = ValueOperationCode.ScaleFloat2; return true;
            default: code = default; return false;
        }
    }

    private static bool TryCompileConstantGetter(
        ValueExpression expression,
        IComparatorValueResolver resolver,
        ref ExpressionProgram program,
        out UnitValueCategory category,
        out string error)
    {
        category = UnitValueCategory.None;
        if (!resolver.TryGet(expression.GetterKey, out IParameterizedUnitValueGetter getter) || getter == null)
        {
            error = $"Getter '{expression.GetterKey}' is unavailable.";
            return false;
        }

        if (getter.Parameters.Count != 0 || expression.Inputs == null || expression.Inputs.Count != 0)
        {
            error = $"Getter '{expression.GetterKey}' is not a generated unmanaged source.";
            return false;
        }

        if (!getter.TryGet(Array.Empty<UnitValue>(), out UnitValue value) ||
            !UnitSourceValue.TryFromUnitValue(value, out UnitSourceValue literal) ||
            !program.TryAddLiteral(in literal, out ushort literalIndex))
        {
            error = $"Getter '{expression.GetterKey}' could not be resolved to a constant value.";
            return false;
        }

        if (!TryAppendInstruction(
                ref program,
                new ExpressionInstruction
                {
                    Kind = ExpressionInstructionKind.Literal,
                    LiteralIndex = literalIndex,
                },
                out error))
        {
            return false;
        }

        category = getter.ReturnType;
        error = string.Empty;
        return true;
    }

    private static bool TryGetCompareCode(ICompareType compare, out CompareOperationCode code)
    {
        switch (compare)
        {
            case Equal: code = CompareOperationCode.Equal; return true;
            case NotEqual: code = CompareOperationCode.NotEqual; return true;
            case GreaterThan: code = CompareOperationCode.GreaterThan; return true;
            case GreaterOrEqual: code = CompareOperationCode.GreaterOrEqual; return true;
            case LessThan: code = CompareOperationCode.LessThan; return true;
            case LessOrEqual: code = CompareOperationCode.LessOrEqual; return true;
            case IsTrue: code = CompareOperationCode.IsTrue; return true;
            case IsFalse: code = CompareOperationCode.IsFalse; return true;
            default: code = default; return false;
        }
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
