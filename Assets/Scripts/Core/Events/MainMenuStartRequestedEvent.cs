namespace CrystalMagic.Core
{
    public readonly struct MainMenuStartRequestedEvent : IGameEvent
    {
        public MainMenuStartRequestedEvent(int index)
        {
            Index = index;
        }

        public int Index { get; }
    }
}
