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
        factory.RegisterSource("UnitHasTargetSource", static () => new UnitHasTargetSource());
        factory.RegisterSource("UnitIsCastingSource", static () => new UnitIsCastingSource());
        factory.RegisterSource("UnitIsControlledSource", static () => new UnitIsControlledSource());
        factory.RegisterSource("UnitIsEnemySource", static () => new UnitIsEnemySource());
        factory.RegisterSource("UnitTargetCastRangeMarginSource", static () => new UnitTargetCastRangeMarginSource());
        factory.RegisterSource("UnitVelocitySource", static () => new UnitVelocitySource());
        factory.RegisterSource("UnitWantToCastSource", static () => new UnitWantToCastSource());

        factory.RegisterCompareType("Equal", static value => new Equal { value = value });
        factory.RegisterCompareType("GreaterThan", static value => new GreaterThan { value = value });
        factory.RegisterCompareType("IsFalse", static _ => new IsFalse());
        factory.RegisterCompareType("IsTrue", static _ => new IsTrue());
        factory.RegisterCompareType("LessThan", static value => new LessThan { value = value });
    }
}
