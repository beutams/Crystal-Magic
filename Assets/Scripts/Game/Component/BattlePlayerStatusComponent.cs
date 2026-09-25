using Unity.Entities;

public enum BattlePlayerLifeState : byte
{
    Alive = 0,
    Dead = 1,
}

public enum BattlePlayerConnectionState : byte
{
    Online = 0,
    Offline = 1,
}

public struct BattlePlayerStatusComponent : IComponentData
{
    public BattlePlayerLifeState LifeState;
    public BattlePlayerConnectionState ConnectionState;
    public byte TransitionReady;
    public byte NetworkDirty;

    public readonly bool IsSpectator =>
        LifeState == BattlePlayerLifeState.Dead ||
        ConnectionState == BattlePlayerConnectionState.Offline;

    public readonly bool IsWaitingForTransition => TransitionReady != 0;
    public readonly bool IsInputLocked => IsSpectator || IsWaitingForTransition;
    public readonly bool IsFaded => IsSpectator || IsWaitingForTransition;
}
