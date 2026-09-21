using Unity.Entities;

[System.Flags]
public enum GameGateMask : byte
{
    None = 0,
    Simulation = 1 << 0,
    PlayerInput = 1 << 1,
    UIInput = 1 << 2,
}

public struct GameGateStateComponent : IComponentData
{
    public GameGateMask Mask;

    public bool IsSimulationLocked => (Mask & GameGateMask.Simulation) != 0;
    public bool IsPlayerInputLocked => (Mask & GameGateMask.PlayerInput) != 0;
    public bool IsUIInputLocked => (Mask & GameGateMask.UIInput) != 0;
}
