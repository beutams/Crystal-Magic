using Unity.Entities;
using Unity.Mathematics;

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

    /// <summary>
    /// Raised once a positive damage value has been applied to a unit.
    /// </summary>
    public readonly struct DamageAppliedEvent : IGameEvent
    {
        public DamageAppliedEvent(Entity targetEntity, float3 worldPosition, float amount, bool isLethal)
        {
            TargetEntity = targetEntity;
            WorldPosition = worldPosition;
            Amount = amount;
            IsLethal = isLethal;
        }

        public Entity TargetEntity { get; }
        public float3 WorldPosition { get; }
        public float Amount { get; }
        public bool IsLethal { get; }
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
