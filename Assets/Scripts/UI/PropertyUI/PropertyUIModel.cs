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
                snapshot.Speed = UnitModifierResolver.GetMoveSpeed(entityManager, player);
            }

            if (entityManager.HasComponent<UnitVitalityComponent>(player))
            {
                snapshot.MaxHealth = UnitModifierResolver.GetMaxHealth(entityManager, player);
                snapshot.HealthRegen = UnitModifierResolver.GetHealthRegen(entityManager, player);
            }

            if (entityManager.HasComponent<UnitManaComponent>(player))
            {
                snapshot.MaxMana = UnitModifierResolver.GetMaxMp(entityManager, player);
                snapshot.ManaRegen = UnitModifierResolver.GetMpRegen(entityManager, player);
            }

            if (entityManager.HasComponent<UnitAttackComponent>(player))
            {
                snapshot.AttackPower = UnitModifierResolver.GetAttackPower(entityManager, player);
                snapshot.ChantSpeed = UnitModifierResolver.GetChantSpeedBonus(entityManager, player);
                snapshot.SkillRange = UnitModifierResolver.GetSkillRange(entityManager, player);
            }

            if (entityManager.HasComponent<UnitElementComponent>(player))
            {
                snapshot.Water = UnitModifierResolver.GetElementPower(entityManager, player, CrystalMagic.Game.Data.Effects.ElementType.Water);
                snapshot.Fire = UnitModifierResolver.GetElementPower(entityManager, player, CrystalMagic.Game.Data.Effects.ElementType.Fire);
                snapshot.Lighting = UnitModifierResolver.GetElementPower(entityManager, player, CrystalMagic.Game.Data.Effects.ElementType.Lightning);
                snapshot.Wind = UnitModifierResolver.GetElementPower(entityManager, player, CrystalMagic.Game.Data.Effects.ElementType.Wind);
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
                entityManager.HasComponent<UnitFactionComponent>(_cachedPlayerEntity) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(_cachedPlayerEntity).Value))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitFactionComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                    continue;

                _cachedPlayerEntity = entity;
                player = entity;
                return true;
            }

            player = Entity.Null;
            return false;
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
                && Mathf.Approximately(ChantSpeed, other.ChantSpeed)
                && Mathf.Approximately(Fire, other.Fire)
                && Mathf.Approximately(Water, other.Water)
                && Mathf.Approximately(Lighting, other.Lighting)
                && Mathf.Approximately(Wind, other.Wind)
                && Mathf.Approximately(SkillRange, other.SkillRange);
        }
    }
}
