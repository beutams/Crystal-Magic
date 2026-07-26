// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/State Machine

public static class StateMachineRegistry
{
    public static void RegisterAll(StateMachineFactory factory)
    {
        if (factory == null)
            return;

        factory.Register("ControlledState", static () => new ControlledState());
        factory.Register("DeathState", static () => new DeathState());
        factory.Register("IdleState", static () => new IdleState());
        factory.Register("MoveState", static () => new MoveState());
        factory.Register("PlayerCastState", static () => new PlayerCastState());
        factory.Register("UnitCastState", static () => new UnitCastState());
    }
}
