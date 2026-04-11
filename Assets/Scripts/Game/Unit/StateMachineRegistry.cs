// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Use menu: Tools/State Machine/Generate State Machine Registry
// Generated: 2026-04-10 17:04:12

public static class StateMachineRegistry
{
    public static void RegisterAll(StateMachineFactory factory)
    {
        // ── AUnitState 子类 ──────────────────────────────────────────
        factory.RegisterState<IdleState>();
        factory.RegisterState<MoveState>();

        // ── ISource 实现 ─────────────────────────────────────────────
        factory.RegisterSource<UnitVelocitySource>();

        // ── ICompareType 实现 ────────────────────────────────────────
        factory.RegisterCompareType<Equal>(v => new Equal { value = v });
        factory.RegisterCompareType<GreaterThan>(v => new GreaterThan { value = v });
        factory.RegisterCompareType<IsFalse>(_ => new IsFalse());
        factory.RegisterCompareType<IsTrue>(_ => new IsTrue());
        factory.RegisterCompareType<LessThan>(v => new LessThan { value = v });
    }
}
