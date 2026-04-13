// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Use menu: Tools/State Machine/Generate State Machine Registry
// Generated: 2026-04-12 00:00:00

public static class StateMachineRegistry
{
    public static void RegisterAll(StateMachineFactory factory, ComparatorFactory comparatorFactory)
    {
        // ── AUnitState 子类 ──────────────────────────────────────────
        factory.RegisterState<IdleState>();
        factory.RegisterState<MoveState>();

        // ── ISource 实现 ─────────────────────────────────────────────
        comparatorFactory.RegisterSource<UnitVelocitySource>();

        // ── ICompareType 实现 ────────────────────────────────────────
        comparatorFactory.RegisterCompareType<Equal>(v => new Equal { value = v });
        comparatorFactory.RegisterCompareType<GreaterThan>(v => new GreaterThan { value = v });
        comparatorFactory.RegisterCompareType<IsFalse>(_ => new IsFalse());
        comparatorFactory.RegisterCompareType<IsTrue>(_ => new IsTrue());
        comparatorFactory.RegisterCompareType<LessThan>(v => new LessThan { value = v });
    }
}
