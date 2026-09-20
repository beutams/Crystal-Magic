using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum ValueOperationCode : byte
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Min,
    Max,
    Abs,
    Clamp,
    Distance,
    DistanceSquared,
    Length,
    Length2,
    LengthSquared,
    Dot,
    ScaleFloat2,
}

public enum CompareOperationCode : byte
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    IsTrue,
    IsFalse,
}

public enum ExpressionInstructionKind : byte
{
    Literal,
    Source,
    Operation,
    Compare,
}

public struct ExpressionInstruction
{
    public ExpressionInstructionKind Kind;
    public byte InputCount;
    public ushort LiteralIndex;
    public UnitSourceId SourceId;
    public UnitSourceTarget SourceTarget;
    public ValueOperationCode Operation;
    public CompareOperationCode Compare;
    public ConditionType ConditionType;
}

internal struct ExpressionProgram
{
    public FixedList4096Bytes<ExpressionInstruction> Instructions;
    public FixedList4096Bytes<UnitSourceValue> Literals;

    public bool TryAddInstruction(in ExpressionInstruction instruction)
    {
        if (Instructions.Length >= Instructions.Capacity)
            return false;

        Instructions.Add(instruction);
        return true;
    }

    public bool TryAddLiteral(in UnitSourceValue value, out ushort index)
    {
        index = 0;
        if (Literals.Length >= Literals.Capacity || Literals.Length > ushort.MaxValue)
            return false;

        index = (ushort)Literals.Length;
        Literals.Add(value);
        return true;
    }
}

public struct CompiledValueExpression
{
    private ExpressionProgram _program;
    private byte _isValid;

    internal CompiledValueExpression(UnitValueCategory category, in ExpressionProgram program)
    {
        Category = category;
        _program = program;
        _isValid = 1;
    }

    public UnitValueCategory Category { get; }
    public bool IsValid => _isValid != 0;
    internal ExpressionProgram Program => _program;

    public bool TryEvaluate(
        Entity entity,
        in UnitSourceDispatcher dispatcher,
        out UnitSourceValue value)
    {
        UnitSourceContext context = new(entity);
        return TryEvaluate(in context, in dispatcher, out value);
    }

    public bool TryEvaluate(
        in UnitSourceContext context,
        in UnitSourceDispatcher dispatcher,
        out UnitSourceValue value)
    {
        value = default;
        return IsValid &&
               CompiledExpressionEvaluator.TryEvaluateValue(in _program, in context, in dispatcher, out value);
    }

    public bool TryEvaluate(UnitSourceResolver resolver, out UnitSourceValue value)
    {
        value = default;
        return resolver != null &&
               resolver.TryGetContext(out UnitSourceContext context, out UnitSourceDispatcher dispatcher) &&
               TryEvaluate(in context, in dispatcher, out value);
    }
}

internal static class CompiledExpressionEvaluator
{
    public static bool TryEvaluateValue(
        ref BehaviorExpressionBlob program,
        in UnitSourceContext context,
        in UnitSourceDispatcher dispatcher,
        out UnitSourceValue value)
    {
        value = default;
        FixedList4096Bytes<UnitSourceValue> stack = default;
        for (int index = 0; index < program.Instructions.Length; index++)
        {
            ExpressionInstruction instruction = program.Instructions[index];
            switch (instruction.Kind)
            {
                case ExpressionInstructionKind.Literal:
                    if (instruction.LiteralIndex >= program.Literals.Length ||
                        !TryPush(ref stack, program.Literals[instruction.LiteralIndex]))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Source:
                    if (!TryEvaluateSource(
                            instruction.SourceId,
                            instruction.SourceTarget,
                            instruction.InputCount,
                            in context,
                            in dispatcher,
                            ref stack))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Operation:
                    if (!TryEvaluateOperation(instruction.Operation, instruction.InputCount, ref stack))
                        return false;
                    break;

                default:
                    return false;
            }
        }

        if (stack.Length != 1)
            return false;

        value = stack[0];
        return value.Category != UnitValueCategory.None;
    }

    public static bool TryEvaluateConditions(
        ref BehaviorExpressionBlob program,
        in UnitSourceContext context,
        in UnitSourceDispatcher dispatcher)
    {
        FixedList4096Bytes<UnitSourceValue> stack = default;
        for (int index = 0; index < program.Instructions.Length; index++)
        {
            ExpressionInstruction instruction = program.Instructions[index];
            switch (instruction.Kind)
            {
                case ExpressionInstructionKind.Literal:
                    if (instruction.LiteralIndex >= program.Literals.Length ||
                        !TryPush(ref stack, program.Literals[instruction.LiteralIndex]))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Source:
                    if (!TryEvaluateSource(
                            instruction.SourceId,
                            instruction.SourceTarget,
                            instruction.InputCount,
                            in context,
                            in dispatcher,
                            ref stack))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Operation:
                    if (!TryEvaluateOperation(instruction.Operation, instruction.InputCount, ref stack))
                        return false;
                    break;

                case ExpressionInstructionKind.Compare:
                    if (!TryEvaluateCompare(
                            instruction.Compare,
                            instruction.InputCount,
                            ref stack,
                            out bool matches))
                    {
                        return false;
                    }

                    if (instruction.ConditionType == ConditionType.Necessary && !matches ||
                        instruction.ConditionType == ConditionType.Unallowed && matches)
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }

        return stack.Length == 0;
    }

    public static bool TryEvaluateValue(
        in ExpressionProgram program,
        in UnitSourceContext context,
        in UnitSourceDispatcher dispatcher,
        out UnitSourceValue value)
    {
        value = default;
        FixedList4096Bytes<UnitSourceValue> stack = default;
        for (int index = 0; index < program.Instructions.Length; index++)
        {
            ExpressionInstruction instruction = program.Instructions[index];
            switch (instruction.Kind)
            {
                case ExpressionInstructionKind.Literal:
                    if (instruction.LiteralIndex >= program.Literals.Length ||
                        !TryPush(ref stack, program.Literals[instruction.LiteralIndex]))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Source:
                    if (!TryEvaluateSource(
                            instruction.SourceId,
                            instruction.SourceTarget,
                            instruction.InputCount,
                            in context,
                            in dispatcher,
                            ref stack))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Operation:
                    if (!TryEvaluateOperation(instruction.Operation, instruction.InputCount, ref stack))
                        return false;
                    break;

                default:
                    return false;
            }
        }

        if (stack.Length != 1)
            return false;

        value = stack[0];
        return value.Category != UnitValueCategory.None;
    }

    public static bool TryEvaluateConditions(
        in ExpressionProgram program,
        in UnitSourceContext context,
        in UnitSourceDispatcher dispatcher)
    {
        FixedList4096Bytes<UnitSourceValue> stack = default;
        for (int index = 0; index < program.Instructions.Length; index++)
        {
            ExpressionInstruction instruction = program.Instructions[index];
            switch (instruction.Kind)
            {
                case ExpressionInstructionKind.Literal:
                    if (instruction.LiteralIndex >= program.Literals.Length ||
                        !TryPush(ref stack, program.Literals[instruction.LiteralIndex]))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Source:
                    if (!TryEvaluateSource(
                            instruction.SourceId,
                            instruction.SourceTarget,
                            instruction.InputCount,
                            in context,
                            in dispatcher,
                            ref stack))
                    {
                        return false;
                    }
                    break;

                case ExpressionInstructionKind.Operation:
                    if (!TryEvaluateOperation(instruction.Operation, instruction.InputCount, ref stack))
                        return false;
                    break;

                case ExpressionInstructionKind.Compare:
                    if (!TryEvaluateCompare(
                            instruction.Compare,
                            instruction.InputCount,
                            ref stack,
                            out bool matches))
                    {
                        return false;
                    }

                    if (instruction.ConditionType == ConditionType.Necessary && !matches ||
                        instruction.ConditionType == ConditionType.Unallowed && matches)
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }

        return stack.Length == 0;
    }

    private static bool TryEvaluateSource(
        UnitSourceId sourceId,
        UnitSourceTarget sourceTarget,
        int inputCount,
        in UnitSourceContext context,
        in UnitSourceDispatcher dispatcher,
        ref FixedList4096Bytes<UnitSourceValue> stack)
    {
        int start = stack.Length - inputCount;
        if (start < 0)
            return false;

        UnitSourceArguments arguments = default;
        if (inputCount > arguments.Values.Capacity)
            return false;

        for (int index = 0; index < inputCount; index++)
            arguments.Values.Add(stack[start + index]);

        stack.Length = start;
        Entity entity = context.Resolve(sourceTarget);
        return dispatcher.TryGet(entity, sourceId, in arguments, out UnitSourceValue value) &&
               TryPush(ref stack, value);
    }

    private static bool TryEvaluateOperation(
        ValueOperationCode operation,
        int inputCount,
        ref FixedList4096Bytes<UnitSourceValue> stack)
    {
        int start = stack.Length - inputCount;
        if (start < 0 || !TryApplyOperation(operation, inputCount, start, in stack, out UnitSourceValue result))
            return false;

        stack.Length = start;
        return TryPush(ref stack, result);
    }

    private static bool TryEvaluateCompare(
        CompareOperationCode operation,
        int inputCount,
        ref FixedList4096Bytes<UnitSourceValue> stack,
        out bool result)
    {
        result = false;
        int start = stack.Length - inputCount;
        if (start < 0 || !TryApplyCompare(operation, inputCount, start, in stack, out result))
            return false;

        stack.Length = start;
        return true;
    }

    private static bool TryApplyOperation(
        ValueOperationCode operation,
        int inputCount,
        int start,
        in FixedList4096Bytes<UnitSourceValue> values,
        out UnitSourceValue result)
    {
        result = default;
        switch (operation)
        {
            case ValueOperationCode.Add:
            case ValueOperationCode.Subtract:
            case ValueOperationCode.Multiply:
            case ValueOperationCode.Divide:
            case ValueOperationCode.Min:
            case ValueOperationCode.Max:
                if (inputCount != 2 ||
                    !values[start].TryGetNumber(out float left) ||
                    !values[start + 1].TryGetNumber(out float right) ||
                    operation == ValueOperationCode.Divide && math.abs(right) <= 0.0001f)
                {
                    return false;
                }

                float number = operation switch
                {
                    ValueOperationCode.Add => left + right,
                    ValueOperationCode.Subtract => left - right,
                    ValueOperationCode.Multiply => left * right,
                    ValueOperationCode.Divide => left / right,
                    ValueOperationCode.Min => math.min(left, right),
                    _ => math.max(left, right),
                };
                result = UnitSourceValue.FromFloat(number);
                return true;

            case ValueOperationCode.Abs:
                if (inputCount != 1 || !values[start].TryGetNumber(out float absoluteValue))
                    return false;
                result = UnitSourceValue.FromFloat(math.abs(absoluteValue));
                return true;

            case ValueOperationCode.Clamp:
                if (inputCount != 3 ||
                    !values[start].TryGetNumber(out float value) ||
                    !values[start + 1].TryGetNumber(out float minimum) ||
                    !values[start + 2].TryGetNumber(out float maximum))
                {
                    return false;
                }
                result = UnitSourceValue.FromFloat(math.clamp(value, minimum, maximum));
                return true;

            case ValueOperationCode.Distance:
            case ValueOperationCode.DistanceSquared:
            case ValueOperationCode.Dot:
                if (inputCount != 2 ||
                    !values[start].TryGetFloat3(out float3 leftVector) ||
                    !values[start + 1].TryGetFloat3(out float3 rightVector))
                {
                    return false;
                }
                result = UnitSourceValue.FromFloat(operation switch
                {
                    ValueOperationCode.Distance => math.distance(leftVector, rightVector),
                    ValueOperationCode.DistanceSquared => math.distancesq(leftVector, rightVector),
                    _ => math.dot(leftVector, rightVector),
                });
                return true;

            case ValueOperationCode.Length:
            case ValueOperationCode.LengthSquared:
                if (inputCount != 1 || !values[start].TryGetFloat3(out float3 vector3))
                    return false;
                result = UnitSourceValue.FromFloat(operation == ValueOperationCode.Length
                    ? math.length(vector3)
                    : math.lengthsq(vector3));
                return true;

            case ValueOperationCode.Length2:
                if (inputCount != 1 || !values[start].TryGetFloat2(out float2 vector2))
                    return false;
                result = UnitSourceValue.FromFloat(math.length(vector2));
                return true;

            case ValueOperationCode.ScaleFloat2:
                if (inputCount != 2 ||
                    !values[start].TryGetFloat2(out float2 scaleVector) ||
                    !values[start + 1].TryGetNumber(out float scale))
                {
                    return false;
                }
                result = UnitSourceValue.FromFloat2(scaleVector * scale);
                return true;

            default:
                return false;
        }
    }

    private static bool TryApplyCompare(
        CompareOperationCode operation,
        int inputCount,
        int start,
        in FixedList4096Bytes<UnitSourceValue> values,
        out bool result)
    {
        result = false;
        switch (operation)
        {
            case CompareOperationCode.Equal:
            case CompareOperationCode.NotEqual:
                if (inputCount != 2)
                    return false;
                bool equals = EqualsValue(values[start], values[start + 1]);
                result = operation == CompareOperationCode.Equal ? equals : !equals;
                return true;

            case CompareOperationCode.GreaterThan:
            case CompareOperationCode.GreaterOrEqual:
            case CompareOperationCode.LessThan:
            case CompareOperationCode.LessOrEqual:
                if (inputCount != 2 ||
                    !values[start].TryGetNumber(out float left) ||
                    !values[start + 1].TryGetNumber(out float right))
                {
                    return false;
                }
                result = operation switch
                {
                    CompareOperationCode.GreaterThan => left > right,
                    CompareOperationCode.GreaterOrEqual => left >= right,
                    CompareOperationCode.LessThan => left < right,
                    _ => left <= right,
                };
                return true;

            case CompareOperationCode.IsTrue:
            case CompareOperationCode.IsFalse:
                if (inputCount != 1)
                    return false;

                UnitSourceValue source = values[start];
                if (source.TryGetBool(out bool boolValue))
                {
                    result = operation == CompareOperationCode.IsTrue ? boolValue : !boolValue;
                    return true;
                }

                if (!source.TryGetNumber(out float number))
                    return false;

                result = operation == CompareOperationCode.IsTrue ? number > 0f : number <= 0f;
                return true;

            default:
                return false;
        }
    }

    private static bool EqualsValue(in UnitSourceValue left, in UnitSourceValue right)
    {
        if (left.TryGetNumber(out float leftNumber) && right.TryGetNumber(out float rightNumber))
            return math.abs(leftNumber - rightNumber) <= 0.0001f;

        if (left.Type != right.Type)
            return false;

        return left.Type switch
        {
            UnitValueType.None => true,
            UnitValueType.Bool => left.Bool == right.Bool,
            UnitValueType.Float2 => math.all(left.Float2 == right.Float2),
            UnitValueType.Float3 => math.all(left.Float3 == right.Float3),
            UnitValueType.Entity => left.Entity == right.Entity,
            UnitValueType.String => left.String.Equals(right.String),
            _ => false,
        };
    }

    private static bool TryPush(
        ref FixedList4096Bytes<UnitSourceValue> stack,
        in UnitSourceValue value)
    {
        if (value.Category == UnitValueCategory.None || stack.Length >= stack.Capacity)
            return false;

        stack.Add(value);
        return true;
    }
}

// These generic constraints deliberately fail compilation if a future edit adds a managed
// field to the data that is expected to cross a Burst/job boundary.
internal static class ExpressionUnmanagedContract
{
    private static void Validate()
    {
        RequireUnmanaged<ExpressionInstruction>();
        RequireUnmanaged<ExpressionProgram>();
        RequireUnmanaged<CompiledValueExpression>();
        RequireUnmanaged<Comparator>();
        RequireUnmanaged<UnitSourceContext>();
        RequireUnmanaged<UnitSourceAccessContext>();
        RequireUnmanaged<UnitSourceDispatcher>();
        RequireUnmanaged<PlayerSkillRuntimeDataComponent>();
        RequireUnmanaged<PlayerSkillChainElement>();
        RequireUnmanaged<PlayerSkillChainSlotElement>();
        RequireUnmanaged<PlayerSkillDefinitionRegistryComponent>();
        RequireUnmanaged<PlayerSkillDefinitionRegistryBlob>();
        RequireUnmanaged<PlayerSkillDefinitionBlob>();
        RequireUnmanaged<PlayerSkillModifierMinimumFactorBlob>();
        RequireUnmanaged<DungeonInterestPointComponent>();
        RequireUnmanaged<DungeonInterestPointCandidateElement>();
        RequireUnmanaged<UnitVariableComponent>();
        RequireUnmanaged<UnitVariableElement>();
        RequireUnmanaged<WorldVariableComponent>();
        RequireUnmanaged<WorldVariableElement>();
    }

    private static void RequireUnmanaged<T>() where T : unmanaged
    {
    }
}
