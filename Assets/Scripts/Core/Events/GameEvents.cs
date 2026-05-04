namespace CrystalMagic.Core {
    /// <summary>
    /// 事件接口定义
    /// </summary>

    public interface IGameEvent { }

    public readonly struct SkillCastLockChangedEvent : IGameEvent
    {
        public SkillCastLockChangedEvent(bool isLocked)
        {
            IsLocked = isLocked;
        }

        public bool IsLocked { get; }
    }

}
