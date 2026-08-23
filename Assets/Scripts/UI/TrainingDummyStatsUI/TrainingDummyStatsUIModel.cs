using System.Collections.Generic;
using System.Text;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class TrainingDummyStatsUIModel : UIModelBase
    {
        public const string DataChangedEventName = "TrainingDummyStatsUIModel.DataChanged";

        private const string PreferredDummyUnitName = "MonsterStraw";
        private const float DpsWindowSeconds = 5f;

        private readonly List<DamageSample> _damageSamples = new();
        private Entity _cachedDummyEntity = Entity.Null;
        private World _cachedQueryWorld;
        private EntityQuery _dummyQuery;
        private string _displayText = string.Empty;
        private float _lastKnownHealth = -1f;
        private float _totalDamage;
        private float _combatStartTime = -1f;
        private bool _hasHealthSnapshot;

        public override string ChangedEventName => DataChangedEventName;
        public string DisplayText => _displayText;

        public void RefreshRuntime()
        {
            float now = Time.time;
            TrimExpiredDamage(now);

            TrainingDummySnapshot snapshot = BuildSnapshot();
            if (snapshot.Exists)
            {
                _lastKnownHealth = snapshot.CurrentHealth;
                _hasHealthSnapshot = true;
            }
            else
            {
                _cachedDummyEntity = Entity.Null;
                _lastKnownHealth = -1f;
                _hasHealthSnapshot = false;
            }

            string nextText = BuildDisplayText(snapshot, now);
            if (string.Equals(_displayText, nextText, System.StringComparison.Ordinal))
                return;

            _displayText = nextText;
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }

        public void HandleUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity))
                return;

            if (gameEvent.TargetEntity != dummyEntity)
                return;

            float damage = 0f;
            if (_hasHealthSnapshot)
            {
                damage = Mathf.Max(0f, _lastKnownHealth - gameEvent.CurrentHealth);
            }
            else if (entityManager.Exists(dummyEntity) && entityManager.HasComponent<UnitVitalityComponent>(dummyEntity))
            {
                damage = Mathf.Max(0f, entityManager.GetComponentData<UnitVitalityComponent>(dummyEntity).CurrentHealth - gameEvent.CurrentHealth);
            }

            _lastKnownHealth = gameEvent.CurrentHealth;
            _hasHealthSnapshot = true;

            if (damage <= 0f)
                return;

            float now = Time.time;
            if (_combatStartTime < 0f)
                _combatStartTime = now;

            _damageSamples.Add(new DamageSample
            {
                Time = now,
                Amount = damage,
            });
            _totalDamage += damage;
            TrimExpiredDamage(now);
        }

        public override void Dispose()
        {
            ReleaseDummyQuery();
            _damageSamples.Clear();
            _cachedDummyEntity = Entity.Null;
            base.Dispose();
        }

        private TrainingDummySnapshot BuildSnapshot()
        {
            if (!TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity))
                return TrainingDummySnapshot.Missing;

            TrainingDummySnapshot snapshot = new()
            {
                Exists = true,
                UnitName = PreferredDummyUnitName,
            };

            if (entityManager.HasComponent<UnitVitalityComponent>(dummyEntity))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(dummyEntity);
                snapshot.CurrentHealth = vitality.CurrentHealth;
                snapshot.MaxHealth = UnitModifierResolver.GetMaxHealth(entityManager, dummyEntity);
                snapshot.HealthRegen = UnitModifierResolver.GetHealthRegen(entityManager, dummyEntity);
                snapshot.Defense = UnitModifierResolver.GetDefense(entityManager, dummyEntity);
            }

            if (entityManager.HasComponent<UnitManaComponent>(dummyEntity))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(dummyEntity);
                snapshot.HasMana = true;
                snapshot.CurrentMana = mana.CurrentMana;
                snapshot.MaxMana = UnitModifierResolver.GetMaxMp(entityManager, dummyEntity);
                snapshot.ManaRegen = UnitModifierResolver.GetMpRegen(entityManager, dummyEntity);
            }

            if (entityManager.HasComponent<UnitAttackComponent>(dummyEntity))
            {
                snapshot.AttackPower = UnitModifierResolver.GetAttackPower(entityManager, dummyEntity);
                snapshot.SkillRange = UnitModifierResolver.GetSkillRange(entityManager, dummyEntity);
                snapshot.ChantSpeedBonus = UnitModifierResolver.GetChantSpeedBonus(entityManager, dummyEntity);
            }

            if (entityManager.HasComponent<UnitMoveComponent>(dummyEntity))
            {
                snapshot.MoveSpeed = UnitModifierResolver.GetMoveSpeed(entityManager, dummyEntity);
                snapshot.MaxAcceleration = UnitModifierResolver.GetMaxAcceleration(entityManager, dummyEntity);
            }

            if (entityManager.HasComponent<UnitBuffRuntimeComponent>(dummyEntity))
            {
                UnitBuffRuntimeComponent runtimeComponent = entityManager.GetComponentObject<UnitBuffRuntimeComponent>(dummyEntity);
                int activeBuffCount = 0;
                for (int i = 0; i < runtimeComponent.Buffs.Count; i++)
                {
                    if (runtimeComponent.Buffs[i].BuffId >= 0 && runtimeComponent.Buffs[i].StackCount > 0)
                        activeBuffCount++;
                }

                snapshot.ActiveBuffCount = activeBuffCount;
            }

            return snapshot;
        }

        private bool TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                dummyEntity = Entity.Null;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedDummyEntity != Entity.Null &&
                entityManager.Exists(_cachedDummyEntity) &&
                entityManager.HasComponent<UnitFactionComponent>(_cachedDummyEntity))
            {
                if (entityManager.GetComponentData<UnitFactionComponent>(_cachedDummyEntity).Value == UnitFactionType.Enemy)
                {
                    dummyEntity = _cachedDummyEntity;
                    return true;
                }
            }

            if (!EnsureDummyQuery(world))
            {
                dummyEntity = Entity.Null;
                return false;
            }

            Entity fallbackEnemy = Entity.Null;
            using NativeArray<Entity> entities = _dummyQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (entityManager.GetComponentData<UnitFactionComponent>(entity).Value != UnitFactionType.Enemy)
                    continue;

                if (fallbackEnemy == Entity.Null)
                    fallbackEnemy = entity;
            }

            if (fallbackEnemy != Entity.Null)
            {
                _cachedDummyEntity = fallbackEnemy;
                dummyEntity = fallbackEnemy;
                return true;
            }

            dummyEntity = Entity.Null;
            return false;
        }

        private bool EnsureDummyQuery(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            if (_cachedQueryWorld == world)
                return true;

            ReleaseDummyQuery();
            _cachedQueryWorld = world;
            _dummyQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<UnitVitalityComponent>());
            return true;
        }

        private void ReleaseDummyQuery()
        {
            if (_cachedQueryWorld != null && _cachedQueryWorld.IsCreated)
                _dummyQuery.Dispose();

            _cachedQueryWorld = null;
            _dummyQuery = default;
        }

        private void TrimExpiredDamage(float now)
        {
            for (int i = _damageSamples.Count - 1; i >= 0; i--)
            {
                if (now - _damageSamples[i].Time <= DpsWindowSeconds)
                    continue;

                _damageSamples.RemoveAt(i);
            }
        }

        private string BuildDisplayText(TrainingDummySnapshot snapshot, float now)
        {
            LocalizationComponent localization = LocalizationComponent.Instance;
            if (!snapshot.Exists)
                return localization.Get("ui.training.stats.missing");

            float windowDamage = 0f;
            for (int i = 0; i < _damageSamples.Count; i++)
                windowDamage += _damageSamples[i].Amount;

            float windowDps = windowDamage / DpsWindowSeconds;
            float averageDps = 0f;
            if (_combatStartTime >= 0f)
            {
                float combatDuration = Mathf.Max(0.001f, now - _combatStartTime);
                averageDps = _totalDamage / combatDuration;
            }

            StringBuilder builder = new(512);
            builder.AppendLine(localization.Get("ui.training.stats.header"));
            builder.AppendLine(localization.Format("ui.training.stats.target", snapshot.UnitName));
            builder.AppendLine(localization.Format("ui.training.stats.health", Format(snapshot.CurrentHealth), Format(snapshot.MaxHealth)));
            if (snapshot.HasMana)
                builder.AppendLine(localization.Format("ui.training.stats.mana", Format(snapshot.CurrentMana), Format(snapshot.MaxMana)));
            else
                builder.AppendLine(localization.Get("ui.training.stats.mana_missing"));

            builder.AppendLine(localization.Format("ui.training.stats.attack", Format(snapshot.AttackPower)));
            builder.AppendLine(localization.Format("ui.training.stats.defense", Format(snapshot.Defense)));
            builder.AppendLine(localization.Format("ui.training.stats.move_speed", Format(snapshot.MoveSpeed)));
            builder.AppendLine(localization.Format("ui.training.stats.max_acceleration", Format(snapshot.MaxAcceleration)));
            builder.AppendLine(localization.Format("ui.training.stats.skill_range", Format(snapshot.SkillRange)));
            builder.AppendLine(localization.Format("ui.training.stats.chant_speed_bonus", Format(snapshot.ChantSpeedBonus)));
            builder.AppendLine(localization.Format("ui.training.stats.health_regen", Format(snapshot.HealthRegen)));
            builder.AppendLine(localization.Format("ui.training.stats.mana_regen", Format(snapshot.ManaRegen)));
            builder.AppendLine(localization.Format("ui.training.stats.active_buff_count", snapshot.ActiveBuffCount));
            builder.AppendLine(localization.Format("ui.training.stats.total_damage", Format(_totalDamage)));
            builder.AppendLine(localization.Format("ui.training.stats.window_dps", Format(windowDps)));
            builder.AppendLine(localization.Format("ui.training.stats.average_dps", Format(averageDps)));
            return builder.ToString();
        }

        private static string Format(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        private struct DamageSample
        {
            public float Time;
            public float Amount;
        }

        private struct TrainingDummySnapshot
        {
            public static TrainingDummySnapshot Missing => default;

            public bool Exists;
            public string UnitName;
            public float CurrentHealth;
            public float MaxHealth;
            public float HealthRegen;
            public float Defense;
            public bool HasMana;
            public float CurrentMana;
            public float MaxMana;
            public float ManaRegen;
            public float AttackPower;
            public float SkillRange;
            public float ChantSpeedBonus;
            public float MoveSpeed;
            public float MaxAcceleration;
            public int ActiveBuffCount;
        }
    }
}
