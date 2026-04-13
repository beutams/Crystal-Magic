namespace CrystalMagic.Core {
    /// <summary>
    /// 事件接口定义
    /// </summary>

    public interface IGameEvent { }

    public readonly struct MainMenuStartRequestedEvent : IGameEvent
    {
    }

    public readonly struct MainMenuLoadRequestedEvent : IGameEvent
    {
        public MainMenuLoadRequestedEvent(string slotName)
        {
            SlotName = slotName;
        }

        public string SlotName { get; }
    }

    public readonly struct MainMenuConfigRequestedEvent : IGameEvent
    {
    }

    public readonly struct MainMenuExitRequestedEvent : IGameEvent
    {
    }
}
