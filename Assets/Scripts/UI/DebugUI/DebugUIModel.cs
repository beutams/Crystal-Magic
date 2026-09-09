using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrystalMagic.UI
{
    public enum DebugPage
    {
        PlayerAttributes,
        TrainingGround,
    }

    public readonly struct DebugPageDefinition
    {
        public DebugPageDefinition(DebugPage page, string title)
        {
            Page = page;
            Title = title;
        }

        public DebugPage Page { get; }
        public string Title { get; }
    }

    public sealed class DebugUIModel : UIModelBase
    {
        public const string DataChangedEventName = "DebugUIModel.DataChanged";

        private const float DamageResetDelaySeconds = 10f;

        private static readonly DebugPageDefinition[] PageDefinitions =
        {
            new(DebugPage.PlayerAttributes, "Player Attributes"),
            new(DebugPage.TrainingGround, "Training Ground"),
        };

        private Entity _cachedPlayerEntity = Entity.Null;
        private Entity _cachedDummyEntity = Entity.Null;
        private World _cachedDummyQueryWorld;
        private EntityQuery _dummyQuery;
        private float _lastKnownDummyHealth = -1f;
        private float _lastDamage;
        private float _sessionDamage;
        private float _damageSessionStartTime = -1f;
        private float _lastDamageTime = -1f;
        private bool _hasDummyHealthSnapshot;

        public override string ChangedEventName => DataChangedEventName;
        public IReadOnlyList<DebugPageDefinition> Pages => PageDefinitions;
        public bool IsContentVisible { get; private set; }
        public DebugPage? SelectedPage { get; private set; }
        public string PlayerAttributesText { get; private set; } = string.Empty;
        public string TrainingGroundText { get; private set; } = string.Empty;

        public void SetContentVisible(bool visible)
        {
            if (IsContentVisible == visible)
                return;

            IsContentVisible = visible;
            if (visible)
                RebuildDisplayText();

            PublishChanged();
        }

        public void SelectPage(DebugPage page)
        {
            if (SelectedPage == page)
                return;

            SelectedPage = page;
            if (IsContentVisible)
                RebuildDisplayText();

            PublishChanged();
        }

        public void RefreshRuntime()
        {
            if (!IsContentVisible || !RebuildDisplayText())
                return;

            PublishChanged();
        }

        public void HandleUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!IsTrainingGroundActive() ||
                !TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity) ||
                gameEvent.TargetEntity != dummyEntity)
                return;

            float damage = 0f;
            if (_hasDummyHealthSnapshot)
            {
                damage = Mathf.Max(0f, _lastKnownDummyHealth - gameEvent.CurrentHealth);
            }
            else if (entityManager.Exists(dummyEntity) && entityManager.HasComponent<UnitVitalityComponent>(dummyEntity))
            {
                damage = Mathf.Max(0f, entityManager.GetComponentData<UnitVitalityComponent>(dummyEntity).CurrentHealth - gameEvent.CurrentHealth);
            }

            _lastKnownDummyHealth = gameEvent.CurrentHealth;
            _hasDummyHealthSnapshot = true;
            if (damage <= 0f)
                return;

            float now = Time.time;
            ResetDamageStatisticsIfExpired(now);
            if (_damageSessionStartTime < 0f)
                _damageSessionStartTime = now;

            _lastDamage = damage;
            _sessionDamage += damage;
            _lastDamageTime = now;
            RefreshRuntime();
        }

        public override void Dispose()
        {
            ReleaseDummyQuery();
            ResetDamageStatistics();
            _cachedPlayerEntity = Entity.Null;
            _cachedDummyEntity = Entity.Null;
            base.Dispose();
        }

        private bool RebuildDisplayText()
        {
            float now = Time.time;
            ResetDamageStatisticsIfExpired(now);

            PlayerAttributesSnapshot playerSnapshot = ReadPlayerAttributes();
            TrainingDummySnapshot trainingSnapshot = ReadTrainingDummy();
            string nextPlayerText = BuildPlayerAttributesText(playerSnapshot);
            string nextTrainingText = BuildTrainingText(trainingSnapshot, now);
            if (string.Equals(PlayerAttributesText, nextPlayerText, StringComparison.Ordinal) &&
                string.Equals(TrainingGroundText, nextTrainingText, StringComparison.Ordinal))
            {
                return false;
            }

            PlayerAttributesText = nextPlayerText;
            TrainingGroundText = nextTrainingText;
            return true;
        }

        private PlayerAttributesSnapshot ReadPlayerAttributes()
        {
            PlayerAttributesSnapshot snapshot = default;
            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                return snapshot;

            snapshot.Exists = true;
            if (entityManager.HasComponent<UnitMoveComponent>(player))
                snapshot.Speed = UnitModifierResolver.GetMoveSpeed(entityManager, player);

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
                snapshot.Lightning = UnitModifierResolver.GetElementPower(entityManager, player, CrystalMagic.Game.Data.Effects.ElementType.Lightning);
                snapshot.Wind = UnitModifierResolver.GetElementPower(entityManager, player, CrystalMagic.Game.Data.Effects.ElementType.Wind);
            }

            return snapshot;
        }

        private TrainingDummySnapshot ReadTrainingDummy()
        {
            if (!IsTrainingGroundActive())
            {
                ResetTrainingSession();
                return default;
            }

            if (!TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity))
            {
                _cachedDummyEntity = Entity.Null;
                _lastKnownDummyHealth = -1f;
                _hasDummyHealthSnapshot = false;
                return default;
            }

            TrainingDummySnapshot snapshot = new()
            {
                Exists = true,
            };

            if (entityManager.HasComponent<UnitVitalityComponent>(dummyEntity))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(dummyEntity);
                snapshot.MaxHealth = UnitModifierResolver.GetMaxHealth(entityManager, dummyEntity);
                snapshot.Defense = UnitModifierResolver.GetDefense(entityManager, dummyEntity);
                _lastKnownDummyHealth = vitality.CurrentHealth;
                _hasDummyHealthSnapshot = true;
            }

            return snapshot;
        }

        private bool IsTrainingGroundActive()
        {
            return SceneManager.GetActiveScene().name == TrainingState.SceneName;
        }

        private void ResetTrainingSession()
        {
            ResetDamageStatistics();
            _cachedDummyEntity = Entity.Null;
            _lastKnownDummyHealth = -1f;
            _hasDummyHealthSnapshot = false;
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
                entityManager.HasComponent<UnitFactionComponent>(_cachedDummyEntity) &&
                entityManager.GetComponentData<UnitFactionComponent>(_cachedDummyEntity).Value == UnitFactionType.Enemy)
            {
                dummyEntity = _cachedDummyEntity;
                return true;
            }

            if (!EnsureDummyQuery(world))
            {
                dummyEntity = Entity.Null;
                return false;
            }

            using NativeArray<Entity> entities = _dummyQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (entityManager.GetComponentData<UnitFactionComponent>(entity).Value != UnitFactionType.Enemy)
                    continue;

                _cachedDummyEntity = entity;
                dummyEntity = entity;
                return true;
            }

            dummyEntity = Entity.Null;
            return false;
        }

        private bool EnsureDummyQuery(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            if (_cachedDummyQueryWorld == world)
                return true;

            ReleaseDummyQuery();
            _cachedDummyQueryWorld = world;
            _dummyQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<UnitVitalityComponent>());
            return true;
        }

        private void ReleaseDummyQuery()
        {
            if (_cachedDummyQueryWorld != null && _cachedDummyQueryWorld.IsCreated)
                _dummyQuery.Dispose();

            _cachedDummyQueryWorld = null;
            _dummyQuery = default;
        }

        private void ResetDamageStatisticsIfExpired(float now)
        {
            if (_lastDamageTime >= 0f && now - _lastDamageTime >= DamageResetDelaySeconds)
                ResetDamageStatistics();
        }

        private void ResetDamageStatistics()
        {
            _lastDamage = 0f;
            _sessionDamage = 0f;
            _damageSessionStartTime = -1f;
            _lastDamageTime = -1f;
        }

        private static string BuildPlayerAttributesText(PlayerAttributesSnapshot snapshot)
        {
            if (!snapshot.Exists)
                return "Player entity is not available.";

            StringBuilder builder = new(384);
            builder.AppendLine("PLAYER ATTRIBUTES");
            builder.AppendLine($"Move Speed: {Format(snapshot.Speed)}");
            builder.AppendLine($"Max Health: {Format(snapshot.MaxHealth)}");
            builder.AppendLine($"Health Regen: {Format(snapshot.HealthRegen)}");
            builder.AppendLine($"Max Mana: {Format(snapshot.MaxMana)}");
            builder.AppendLine($"Mana Regen: {Format(snapshot.ManaRegen)}");
            builder.AppendLine($"Attack Power: {Format(snapshot.AttackPower)}");
            builder.AppendLine($"Chant Speed Bonus: {Format(snapshot.ChantSpeed)}");
            builder.AppendLine($"Fire: {Format(snapshot.Fire)}");
            builder.AppendLine($"Water: {Format(snapshot.Water)}");
            builder.AppendLine($"Lightning: {Format(snapshot.Lightning)}");
            builder.AppendLine($"Wind: {Format(snapshot.Wind)}");
            builder.AppendLine($"Skill Range: {Format(snapshot.SkillRange)}");
            return builder.ToString();
        }

        private string BuildTrainingText(TrainingDummySnapshot snapshot, float now)
        {
            LocalizationComponent localization = LocalizationComponent.Instance;
            if (!snapshot.Exists)
                return localization.Get("ui.training.stats.missing");

            float averageDps = 0f;
            if (_damageSessionStartTime >= 0f)
                averageDps = _sessionDamage / Mathf.Max(0.001f, now - _damageSessionStartTime);

            StringBuilder builder = new(192);
            builder.AppendLine(localization.Get("ui.training.stats.header"));
            builder.AppendLine(localization.Format("ui.training.stats.max_health", Format(snapshot.MaxHealth)));
            builder.AppendLine(localization.Format("ui.training.stats.defense", Format(snapshot.Defense)));
            builder.AppendLine(localization.Format("ui.training.stats.last_damage", Format(_lastDamage)));
            builder.AppendLine(localization.Format("ui.training.stats.average_dps", Format(averageDps)));
            return builder.ToString();
        }

        private static string Format(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }

        private struct PlayerAttributesSnapshot
        {
            public bool Exists;
            public float Speed;
            public float MaxHealth;
            public float HealthRegen;
            public float MaxMana;
            public float ManaRegen;
            public float AttackPower;
            public float ChantSpeed;
            public float Fire;
            public float Water;
            public float Lightning;
            public float Wind;
            public float SkillRange;
        }

        private struct TrainingDummySnapshot
        {
            public bool Exists;
            public float MaxHealth;
            public float Defense;
        }
    }
}
