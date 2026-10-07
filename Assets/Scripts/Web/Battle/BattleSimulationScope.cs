using Unity.Entities;

// Unbound (including standalone) worlds retain their existing update loop.
public struct BattleSimulationScope : IComponentData
{
    public BattleSimulationPass Pass;
    public readonly bool Includes(bool isPlayer) =>
        Pass == BattleSimulationPass.All || (Pass == BattleSimulationPass.Players) == isPlayer;
}

public enum BattleSimulationPass : byte
{
    All,
    World,
    Players,
}
