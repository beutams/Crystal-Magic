using Unity.Entities;

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

    public readonly struct UnitDamagedEvent : IGameEvent
    {
        public UnitDamagedEvent(Entity targetEntity, float currentHealth, float maxHealth)
        {
            TargetEntity = targetEntity;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
        }

        public Entity TargetEntity { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
    }

    public readonly struct UnitDiedEvent : IGameEvent
    {
        public UnitDiedEvent(Entity entity)
        {
            Entity = entity;
        }

        public Entity Entity { get; }
    }

}
