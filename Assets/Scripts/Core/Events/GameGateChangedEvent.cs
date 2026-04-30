namespace CrystalMagic.Core
{
    public readonly struct GameGateChangedEvent : IGameEvent
    {
        public GameGateChangedEvent(GameGateType gateType, bool isLocked)
        {
            GateType = gateType;
            IsLocked = isLocked;
        }

        public GameGateType GateType { get; }
        public bool IsLocked { get; }
    }
}
