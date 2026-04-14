namespace CrystalMagic.Core
{
    public readonly struct MainMenuLoadRequestedEvent : IGameEvent
    {
        public MainMenuLoadRequestedEvent(int index)
        {
            Index = index;
        }

        public int Index { get; }
    }
}
