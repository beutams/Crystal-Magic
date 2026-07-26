using Unity.Entities;

public enum UnitDeathPhase : byte
{
    None = 0,
    PlayingAnimation = 1,
    Completed = 2,
}

public struct UnitDeathComponent : IComponentData, IEnableableComponent
{
    public UnitDeathPhase Phase;
    public float ElapsedSeconds;
}
