using System;
using System.Collections.Generic;
using System.Text;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
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
        UnitSpawner,
        UnitInspector,
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

        private static readonly DebugPageDefinition PlayerAttributesPage = new(DebugPage.PlayerAttributes, "Player Attributes");
        private static readonly DebugPageDefinition TrainingGroundPage = new(DebugPage.TrainingGround, "Training Ground");
        private static readonly DebugPageDefinition UnitSpawnerPage = new(DebugPage.UnitSpawner, "Unit Spawner");
        private static readonly DebugPageDefinition UnitInspectorPage = new(DebugPage.UnitInspector, "Unit Inspector");

        private readonly List<DebugPageDefinition> _pages = new() { PlayerAttributesPage };
        private readonly List<string> _spawnableUnitNames = new();
        private readonly List<UnitDebugUnitEntry> _inspectorUnits = new();
        private readonly List<UnitDebugComponentEntry> _inspectorComponents = new();
        private readonly List<UnitDebugBuffEntry> _inspectorBuffs = new();
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
        private bool _isTrainingGroundActive;
        private int _selectedSpawnUnitIndex = -1;
        private int _selectedInspectorUnitIndex = -1;
        private int _selectedInspectorComponentIndex = -1;
        private int _selectedInspectorBuffIndex = -1;

        public override string ChangedEventName => DataChangedEventName;
        public IReadOnlyList<DebugPageDefinition> Pages => _pages;
        public bool IsContentVisible { get; private set; }
        public bool IsTrainingGroundActive => _isTrainingGroundActive;
        public DebugPage? SelectedPage { get; private set; }
        public string PlayerAttributesText { get; private set; } = string.Empty;
        public string TrainingGroundText { get; private set; } = string.Empty;
        public string SelectedSpawnUnitName => _selectedSpawnUnitIndex >= 0 && _selectedSpawnUnitIndex < _spawnableUnitNames.Count
            ? _spawnableUnitNames[_selectedSpawnUnitIndex]
            : string.Empty;
        public string UnitSpawnerText => string.IsNullOrEmpty(SelectedSpawnUnitName)
            ? "No registered unit is available."
            : $"Unit: {SelectedSpawnUnitName}\n{_selectedSpawnUnitIndex + 1}/{_spawnableUnitNames.Count}";
        public string UnitInspectorUnitText => TryGetSelectedInspectorUnit(out UnitDebugUnitEntry unit)
            ? $"Unit {_selectedInspectorUnitIndex + 1}/{_inspectorUnits.Count}: {unit.Label}"
            : "No live unit is available.";
        public string UnitInspectorComponentText => TryGetSelectedInspectorComponent(out UnitDebugComponentEntry component)
            ? $"Component {_selectedInspectorComponentIndex + 1}/{_inspectorComponents.Count}: {component.Label}"
            : "No editable Component is available.";
        public string UnitInspectorBuffText => TryGetSelectedInspectorBuff(out UnitDebugBuffEntry buff)
            ? $"Buff {_selectedInspectorBuffIndex + 1}/{_inspectorBuffs.Count}: {buff.Label}"
            : "No Buff selected.";
        public string UnitInspectorDetailText { get; private set; } = string.Empty;
        public string UnitInspectorStatusText { get; private set; } = string.Empty;
        public int UnitInspectorEditorRevision { get; private set; }
        public int SelectedInspectorBuffIndex => _selectedInspectorBuffIndex;

        public void SetContentVisible(bool visible)
        {
            if (IsContentVisible == visible)
                return;

            IsContentVisible = visible;
            RefreshPageDefinitions();
            if (visible)
                RebuildDisplayText();

            PublishChanged();
        }

        public void SelectPage(DebugPage page)
        {
            if (!ContainsPage(page))
                return;

            if (SelectedPage == page)
                return;

            SelectedPage = page;
            if (IsContentVisible)
                RebuildDisplayText();

            PublishChanged();
        }

        public void RefreshRuntime()
        {
            bool pageDefinitionsChanged = RefreshPageDefinitions();
            if (!IsContentVisible)
            {
                if (pageDefinitionsChanged)
                    PublishChanged();
                return;
            }

            if (!pageDefinitionsChanged && !RebuildDisplayText())
                return;

            PublishChanged();
        }

        public void SetSpawnableUnitNames(IReadOnlyList<string> unitNames)
        {
            string selectedUnitName = SelectedSpawnUnitName;
            bool changed = _spawnableUnitNames.Count != unitNames.Count;
            if (!changed)
            {
                for (int i = 0; i < unitNames.Count; i++)
                {
                    if (string.Equals(_spawnableUnitNames[i], unitNames[i], StringComparison.Ordinal))
                        continue;

                    changed = true;
                    break;
                }
            }

            if (!changed)
                return;

            _spawnableUnitNames.Clear();
            for (int i = 0; i < unitNames.Count; i++)
                _spawnableUnitNames.Add(unitNames[i]);

            _selectedSpawnUnitIndex = _spawnableUnitNames.IndexOf(selectedUnitName);
            if (_selectedSpawnUnitIndex < 0 && _spawnableUnitNames.Count > 0)
                _selectedSpawnUnitIndex = 0;

            PublishChanged();
        }

        public void SelectPreviousSpawnUnit()
        {
            if (_spawnableUnitNames.Count <= 1)
                return;

            _selectedSpawnUnitIndex = (_selectedSpawnUnitIndex - 1 + _spawnableUnitNames.Count) % _spawnableUnitNames.Count;
            PublishChanged();
        }

        public void SelectNextSpawnUnit()
        {
            if (_spawnableUnitNames.Count <= 1)
                return;

            _selectedSpawnUnitIndex = (_selectedSpawnUnitIndex + 1) % _spawnableUnitNames.Count;
            PublishChanged();
        }

        public void SetInspectorUnits(IReadOnlyList<UnitDebugUnitEntry> units)
        {
            Entity selectedEntity = TryGetSelectedInspectorUnit(out UnitDebugUnitEntry selected) ? selected.Entity : Entity.Null;
            _inspectorUnits.Clear();
            for (int i = 0; i < units.Count; i++)
                _inspectorUnits.Add(units[i]);

            _selectedInspectorUnitIndex = _inspectorUnits.FindIndex(entry => entry.Entity == selectedEntity);
            if (_selectedInspectorUnitIndex < 0 && _inspectorUnits.Count > 0)
                _selectedInspectorUnitIndex = 0;
            _selectedInspectorComponentIndex = -1;
            _selectedInspectorBuffIndex = -1;
            PublishChanged();
        }

        public void SetInspectorComponents(IReadOnlyList<UnitDebugComponentEntry> components)
        {
            UnitDebugComponentKind? selectedKind = TryGetSelectedInspectorComponent(out UnitDebugComponentEntry selected)
                ? selected.Kind
                : null;
            _inspectorComponents.Clear();
            for (int i = 0; i < components.Count; i++)
                _inspectorComponents.Add(components[i]);

            _selectedInspectorComponentIndex = selectedKind.HasValue
                ? _inspectorComponents.FindIndex(entry => entry.Kind == selectedKind.Value)
                : -1;
            if (_selectedInspectorComponentIndex < 0 && _inspectorComponents.Count > 0)
                _selectedInspectorComponentIndex = 0;
            _selectedInspectorBuffIndex = -1;
            PublishChanged();
        }

        public void SetInspectorBuffs(IReadOnlyList<UnitDebugBuffEntry> buffs)
        {
            int selectedBuffId = TryGetSelectedInspectorBuff(out UnitDebugBuffEntry selected) ? selected.BuffId : -1;
            _inspectorBuffs.Clear();
            for (int i = 0; i < buffs.Count; i++)
                _inspectorBuffs.Add(buffs[i]);

            _selectedInspectorBuffIndex = _inspectorBuffs.FindIndex(entry => entry.BuffId == selectedBuffId);
            if (_selectedInspectorBuffIndex < 0 && _inspectorBuffs.Count > 0)
                _selectedInspectorBuffIndex = 0;
            PublishChanged();
        }

        public void SetUnitInspectorDetail(string detail, string status = "")
        {
            bool detailChanged = !string.Equals(UnitInspectorDetailText, detail, StringComparison.Ordinal);
            bool statusChanged = !string.Equals(UnitInspectorStatusText, status, StringComparison.Ordinal);
            if (!detailChanged && !statusChanged)
                return;

            UnitInspectorDetailText = detail ?? string.Empty;
            UnitInspectorStatusText = status ?? string.Empty;
            if (detailChanged)
                UnitInspectorEditorRevision++;
            PublishChanged();
        }

        public void SetUnitInspectorStatus(string status)
        {
            if (string.Equals(UnitInspectorStatusText, status, StringComparison.Ordinal))
                return;

            UnitInspectorStatusText = status ?? string.Empty;
            PublishChanged();
        }

        public bool SelectPreviousInspectorUnit() => SelectInspectorItem(_inspectorUnits.Count, ref _selectedInspectorUnitIndex, -1);
        public bool SelectNextInspectorUnit() => SelectInspectorItem(_inspectorUnits.Count, ref _selectedInspectorUnitIndex, 1);
        public bool SelectPreviousInspectorComponent() => SelectInspectorItem(_inspectorComponents.Count, ref _selectedInspectorComponentIndex, -1);
        public bool SelectNextInspectorComponent() => SelectInspectorItem(_inspectorComponents.Count, ref _selectedInspectorComponentIndex, 1);
        public bool SelectPreviousInspectorBuff() => SelectInspectorItem(_inspectorBuffs.Count, ref _selectedInspectorBuffIndex, -1);
        public bool SelectNextInspectorBuff() => SelectInspectorItem(_inspectorBuffs.Count, ref _selectedInspectorBuffIndex, 1);

        public bool TryGetSelectedInspectorUnit(out UnitDebugUnitEntry entry)
        {
            if (_selectedInspectorUnitIndex >= 0 && _selectedInspectorUnitIndex < _inspectorUnits.Count)
            {
                entry = _inspectorUnits[_selectedInspectorUnitIndex];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryGetSelectedInspectorComponent(out UnitDebugComponentEntry entry)
        {
            if (_selectedInspectorComponentIndex >= 0 && _selectedInspectorComponentIndex < _inspectorComponents.Count)
            {
                entry = _inspectorComponents[_selectedInspectorComponentIndex];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryGetSelectedInspectorBuff(out UnitDebugBuffEntry entry)
        {
            if (_selectedInspectorBuffIndex >= 0 && _selectedInspectorBuffIndex < _inspectorBuffs.Count)
            {
                entry = _inspectorBuffs[_selectedInspectorBuffIndex];
                return true;
            }

            entry = default;
            return false;
        }

        public void HandleUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!IsTrainingGroundSceneActive() ||
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
            if (!IsTrainingGroundSceneActive())
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

        private bool IsTrainingGroundSceneActive()
        {
            return SceneManager.GetActiveScene().name == TrainingState.SceneName;
        }

        private bool RefreshPageDefinitions()
        {
            bool isTrainingGroundActive = IsTrainingGroundSceneActive();
            if (_isTrainingGroundActive == isTrainingGroundActive)
                return false;

            _isTrainingGroundActive = isTrainingGroundActive;
            _pages.Clear();
            _pages.Add(PlayerAttributesPage);
            if (_isTrainingGroundActive)
            {
                _pages.Add(TrainingGroundPage);
                _pages.Add(UnitSpawnerPage);
                _pages.Add(UnitInspectorPage);
            }

            if (SelectedPage.HasValue && !ContainsPage(SelectedPage.Value))
                SelectedPage = null;

            return true;
        }

        private bool ContainsPage(DebugPage page)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                if (_pages[i].Page == page)
                    return true;
            }

            return false;
        }

        private bool SelectInspectorItem(int count, ref int selectedIndex, int direction)
        {
            if (count <= 1)
                return false;

            selectedIndex = (selectedIndex + direction + count) % count;
            PublishChanged();
            return true;
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
