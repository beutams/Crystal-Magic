using Unity.Entities;

public struct Comparator
{
    private ExpressionProgram _program;
    private byte _isValid;

    internal Comparator(in ExpressionProgram program, bool isValid = true)
    {
        _program = program;
        _isValid = isValid ? (byte)1 : (byte)0;
    }

    public bool IsValid => _isValid != 0;
    internal ExpressionProgram Program => _program;

    public bool GetResult(Entity entity, in UnitSourceDispatcher dispatcher)
    {
        UnitSourceContext context = new(entity);
        return GetResult(in context, in dispatcher);
    }

    public bool GetResult(in UnitSourceContext context, in UnitSourceDispatcher dispatcher)
    {
        return IsValid &&
               CompiledExpressionEvaluator.TryEvaluateConditions(in _program, in context, in dispatcher);
    }

    public bool GetResult(UnitSourceResolver resolver)
    {
        return resolver != null &&
               resolver.TryGetContext(out UnitSourceContext context, out UnitSourceDispatcher dispatcher) &&
               GetResult(in context, in dispatcher);
    }
}
