// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Comparator

public static class ComparatorRegistry
{
    public static void RegisterAll(ComparatorFactory factory)
    {
        if (factory == null)
            return;

        factory.RegisterSource("UnitBuffStackSource", static () => new UnitBuffStackSource());
        factory.RegisterSource("UnitCanStartCastSource", static () => new UnitCanStartCastSource());
        factory.RegisterSource("UnitHealthRatioSource", static () => new UnitHealthRatioSource());
        factory.RegisterSource("UnitHasTargetSource", static () => new UnitHasTargetSource());
        factory.RegisterSource("UnitIsCastingSource", static () => new UnitIsCastingSource());
        factory.RegisterSource("UnitIsControlledSource", static () => new UnitIsControlledSource());
        factory.RegisterSource("UnitTargetCastRangeMarginSource", static () => new UnitTargetCastRangeMarginSource());
        factory.RegisterCompareType("Equal", static () => new Equal());
        factory.RegisterCompareType("GreaterOrEqual", static () => new GreaterOrEqual());
        factory.RegisterCompareType("GreaterThan", static () => new GreaterThan());
        factory.RegisterCompareType("IsFalse", static () => new IsFalse());
        factory.RegisterCompareType("IsTrue", static () => new IsTrue());
        factory.RegisterCompareType("LessOrEqual", static () => new LessOrEqual());
        factory.RegisterCompareType("LessThan", static () => new LessThan());
        factory.RegisterCompareType("NotEqual", static () => new NotEqual());

        factory.RegisterValueOperation("Abs", static () => new AbsOperation());
        factory.RegisterValueOperation("Add", static () => new AddOperation());
        factory.RegisterValueOperation("Clamp", static () => new ClampOperation());
        factory.RegisterValueOperation("Distance", static () => new DistanceOperation());
        factory.RegisterValueOperation("DistanceSquared", static () => new DistanceSquaredOperation());
        factory.RegisterValueOperation("Divide", static () => new DivideOperation());
        factory.RegisterValueOperation("Dot", static () => new DotOperation());
        factory.RegisterValueOperation("Length", static () => new LengthOperation());
        factory.RegisterValueOperation("LengthSquared", static () => new LengthSquaredOperation());
        factory.RegisterValueOperation("Max", static () => new MaxOperation());
        factory.RegisterValueOperation("Min", static () => new MinOperation());
        factory.RegisterValueOperation("Multiply", static () => new MultiplyOperation());
        factory.RegisterValueOperation("Subtract", static () => new SubtractOperation());
    }
}
