using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class PropertyUIModel : UIModelBase
    {
        public const string DataChangedEventName = "PropertyUIModel.DataChanged";

        private Entity _cachedPlayerEntity = Entity.Null;
        private PropertySnapshot _snapshot;

        public override string ChangedEventName => DataChangedEventName;

        public float Speed => _snapshot.Speed;
        public float MaxHealth => _snapshot.MaxHealth;
        public float HealthRegen => _snapshot.HealthRegen;
        public float MaxMana => _snapshot.MaxMana;
        public float ManaRegen => _snapshot.ManaRegen;
        public float AttackPower => _snapshot.AttackPower;
        public float ActionSpeed => _snapshot.ActionSpeed;
        public float ChantSpeed => _snapshot.ChantSpeed;
        public float Fire => _snapshot.Fire;
        public float Water => _snapshot.Water;
        public float Lighting => _snapshot.Lighting;
        public float Wind => _snapshot.Wind;
        public float SkillRange => _snapshot.SkillRange;

        public void Refresh()
        {
            RebuildState(publishIfChanged: false);
            PublishChanged();
        }

        public void RefreshRuntime()
        {
            RebuildState(publishIfChanged: true);
        }

        private void RebuildState(bool publishIfChanged)
        {
            PropertySnapshot nextSnapshot = ReadSnapshot();
            if (nextSnapshot.Equals(_snapshot))
                return;

            _snapshot = nextSnapshot;
            if (publishIfChanged)
                PublishChanged();
        }

        private PropertySnapshot ReadSnapshot()
        {
            PropertySnapshot snapshot = default;
            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                return snapshot;

            if (entityManager.HasComponent<UnitMoveComponent>(player))
            {
                UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(player);
                snapshot.Speed = move.RealMoveSpeed;
            }

            if (entityManager.HasComponent<UnitVitalityComponent>(player))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(player);
                snapshot.MaxHealth = vitality.RealMaxHealth;
                snapshot.HealthRegen = vitality.RealHealthRegenPerSecond;
            }

            if (entityManager.HasComponent<UnitManaComponent>(player))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(player);
                snapshot.MaxMana = mana.RealMaxMp;
                snapshot.ManaRegen = mana.RealMpRegenPerSecond;
            }

            if (entityManager.HasComponent<UnitAttackComponent>(player))
            {
                UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(player);
                snapshot.AttackPower = attack.RealAttackPower;
                snapshot.ActionSpeed = attack.RealActionSpeedBonus;
                snapshot.ChantSpeed = attack.RealChantSpeedBonus;
                snapshot.SkillRange = attack.RealSkillRange;
            }

            if (entityManager.HasComponent<UnitElementComponent>(player))
            {
                UnitElementComponent element = entityManager.GetComponentData<UnitElementComponent>(player);
                snapshot.Water = element.WaterPower;
                snapshot.Fire = element.FirePower;
                snapshot.Lighting = element.LightningPower;
                snapshot.Wind = element.WindPower;
            }

            return snapshot;
        }

        private bool TryGetPlayerEntity(out EntityManager entityManager, out Entity player)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                player = Entity.Null;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedPlayerEntity != Entity.Null &&
                entityManager.Exists(_cachedPlayerEntity) &&
                entityManager.HasComponent<PlayerTag>(_cachedPlayerEntity))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length <= 0)
            {
                player = Entity.Null;
                return false;
            }

            _cachedPlayerEntity = entities[0];
            player = _cachedPlayerEntity;
            return true;
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }

    internal struct PropertySnapshot
    {
        public float Speed;
        public float MaxHealth;
        public float HealthRegen;
        public float MaxMana;
        public float ManaRegen;
        public float AttackPower;
        public float ActionSpeed;
        public float ChantSpeed;
        public float Fire;
        public float Water;
        public float Lighting;
        public float Wind;
        public float SkillRange;

        public bool Equals(PropertySnapshot other)
        {
            return Mathf.Approximately(Speed, other.Speed)
                && Mathf.Approximately(MaxHealth, other.MaxHealth)
                && Mathf.Approximately(HealthRegen, other.HealthRegen)
                && Mathf.Approximately(MaxMana, other.MaxMana)
                && Mathf.Approximately(ManaRegen, other.ManaRegen)
                && Mathf.Approximately(AttackPower, other.AttackPower)
                && Mathf.Approximately(ActionSpeed, other.ActionSpeed)
                && Mathf.Approximately(ChantSpeed, other.ChantSpeed)
                && Mathf.Approximately(Fire, other.Fire)
                && Mathf.Approximately(Water, other.Water)
                && Mathf.Approximately(Lighting, other.Lighting)
                && Mathf.Approximately(Wind, other.Wind)
                && Mathf.Approximately(SkillRange, other.SkillRange);
        }
    }
}
