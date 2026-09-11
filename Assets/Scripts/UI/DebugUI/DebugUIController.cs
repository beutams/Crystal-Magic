using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.UI
{
    public sealed class DebugUIController : UIControllerBase<DebugUI, DebugUIModel>
    {
        private const float UnitSpawnDistance = 4f;

        private readonly List<string> _registeredUnitNames = new();
        private readonly List<UnitDebugUnitEntry> _inspectorUnits = new();
        private readonly List<UnitDebugComponentEntry> _inspectorComponents = new();
        private readonly List<UnitDebugBuffEntry> _inspectorBuffs = new();

        public DebugUIController(DebugUI view, DebugUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(
                () => View.ContentToggleRequested += HandleContentToggleRequested,
                () => View.ContentToggleRequested -= HandleContentToggleRequested);
            Bindings.Bind(
                () => View.ContentHideRequested += HandleContentHideRequested,
                () => View.ContentHideRequested -= HandleContentHideRequested);
            Bindings.Bind(
                () => View.PageRequested += HandlePageRequested,
                () => View.PageRequested -= HandlePageRequested);
            Bindings.Bind(
                () => View.PreviousSpawnUnitRequested += HandlePreviousSpawnUnitRequested,
                () => View.PreviousSpawnUnitRequested -= HandlePreviousSpawnUnitRequested);
            Bindings.Bind(
                () => View.NextSpawnUnitRequested += HandleNextSpawnUnitRequested,
                () => View.NextSpawnUnitRequested -= HandleNextSpawnUnitRequested);
            Bindings.Bind(
                () => View.SpawnUnitRequested += HandleSpawnUnitRequested,
                () => View.SpawnUnitRequested -= HandleSpawnUnitRequested);
            Bindings.Bind(
                () => View.UnitInspectorRefreshRequested += HandleUnitInspectorRefreshRequested,
                () => View.UnitInspectorRefreshRequested -= HandleUnitInspectorRefreshRequested);
            Bindings.Bind(
                () => View.PreviousInspectorUnitRequested += HandlePreviousInspectorUnitRequested,
                () => View.PreviousInspectorUnitRequested -= HandlePreviousInspectorUnitRequested);
            Bindings.Bind(
                () => View.NextInspectorUnitRequested += HandleNextInspectorUnitRequested,
                () => View.NextInspectorUnitRequested -= HandleNextInspectorUnitRequested);
            Bindings.Bind(
                () => View.PreviousInspectorComponentRequested += HandlePreviousInspectorComponentRequested,
                () => View.PreviousInspectorComponentRequested -= HandlePreviousInspectorComponentRequested);
            Bindings.Bind(
                () => View.NextInspectorComponentRequested += HandleNextInspectorComponentRequested,
                () => View.NextInspectorComponentRequested -= HandleNextInspectorComponentRequested);
            Bindings.Bind(
                () => View.PreviousInspectorBuffRequested += HandlePreviousInspectorBuffRequested,
                () => View.PreviousInspectorBuffRequested -= HandlePreviousInspectorBuffRequested);
            Bindings.Bind(
                () => View.NextInspectorBuffRequested += HandleNextInspectorBuffRequested,
                () => View.NextInspectorBuffRequested -= HandleNextInspectorBuffRequested);
            Bindings.Bind(
                () => View.UnitInspectorApplyRequested += HandleUnitInspectorApplyRequested,
                () => View.UnitInspectorApplyRequested -= HandleUnitInspectorApplyRequested);
            Bindings.Bind(
                () => View.UnitInspectorAddBuffRequested += HandleUnitInspectorAddBuffRequested,
                () => View.UnitInspectorAddBuffRequested -= HandleUnitInspectorAddBuffRequested);
            Bindings.Bind(
                () => View.UnitInspectorRemoveBuffRequested += HandleUnitInspectorRemoveBuffRequested,
                () => View.UnitInspectorRemoveBuffRequested -= HandleUnitInspectorRemoveBuffRequested);
            Bindings.Bind(
                () => View.UnitInspectorClearBehaviorRequested += HandleUnitInspectorClearBehaviorRequested,
                () => View.UnitInspectorClearBehaviorRequested -= HandleUnitInspectorClearBehaviorRequested);
            Bindings.Bind(
                () => View.UnitInspectorClearStateScriptRequested += HandleUnitInspectorClearStateScriptRequested,
                () => View.UnitInspectorClearStateScriptRequested -= HandleUnitInspectorClearStateScriptRequested);
            BindEvent<UnitDamagedEvent>(Model.HandleUnitDamaged);
        }

        protected override void OnUpdate()
        {
            Model.RefreshRuntime();
        }

        private void HandleContentToggleRequested()
        {
            Model.SetContentVisible(!Model.IsContentVisible);
        }

        private void HandleContentHideRequested()
        {
            Model.SetContentVisible(false);
        }

        private void HandlePageRequested(DebugPage page)
        {
            Model.SelectPage(page);

            if (page == DebugPage.UnitSpawner)
                RefreshRegisteredUnitNames();
            else if (page == DebugPage.UnitInspector)
                RefreshUnitInspector();
        }

        private void HandlePreviousSpawnUnitRequested() => Model.SelectPreviousSpawnUnit();

        private void HandleNextSpawnUnitRequested() => Model.SelectNextSpawnUnit();

        private void HandleSpawnUnitRequested()
        {
            if (!DebugComponent.Instance.IsEnabled ||
                !Model.IsTrainingGroundActive ||
                string.IsNullOrEmpty(Model.SelectedSpawnUnitName) ||
                !TryGetPlayerSpawnContext(out EntityManager entityManager, out float3 spawnPosition, out float2 spawnFacing))
            {
                return;
            }

            if (!EntitySpawnRegistryUtility.TryInstantiateUnit(
                    entityManager,
                    new FixedString128Bytes(Model.SelectedSpawnUnitName),
                    out Entity unitEntity))
            {
                return;
            }

            if (!entityManager.HasComponent<LocalTransform>(unitEntity))
                return;

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(unitEntity);
            transform.Position = spawnPosition;
            transform.Rotation = UnitFacingUtility.CreateRotation(-spawnFacing);
            entityManager.SetComponentData(unitEntity, transform);
            UnitFacingUtility.SetFacing(entityManager, unitEntity, -spawnFacing);
        }

        private void RefreshRegisteredUnitNames()
        {
            _registeredUnitNames.Clear();

            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
                EntitySpawnRegistryUtility.GetRegisteredUnitNames(world.EntityManager, _registeredUnitNames);

            Model.SetSpawnableUnitNames(_registeredUnitNames);
        }

        private static bool TryGetPlayerSpawnContext(
            out EntityManager entityManager,
            out float3 spawnPosition,
            out float2 spawnFacing)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                spawnPosition = default;
                spawnFacing = default;
                return false;
            }

            entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                    continue;

                LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(entity);
                UnitFacingUtility.TryGetFacing(entityManager, entity, out spawnFacing);
                spawnPosition = playerTransform.Position + new float3(spawnFacing.x, spawnFacing.y, 0f) * UnitSpawnDistance;
                return true;
            }

            spawnPosition = default;
            spawnFacing = default;
            return false;
        }

        private void HandleUnitInspectorRefreshRequested()
        {
            RefreshUnitInspector();
        }

        private void HandlePreviousInspectorUnitRequested()
        {
            if (Model.SelectPreviousInspectorUnit())
                RefreshSelectedInspectorData();
        }

        private void HandleNextInspectorUnitRequested()
        {
            if (Model.SelectNextInspectorUnit())
                RefreshSelectedInspectorData();
        }

        private void HandlePreviousInspectorComponentRequested()
        {
            if (Model.SelectPreviousInspectorComponent())
                RefreshSelectedInspectorData();
        }

        private void HandleNextInspectorComponentRequested()
        {
            if (Model.SelectNextInspectorComponent())
                RefreshSelectedInspectorData();
        }

        private void HandlePreviousInspectorBuffRequested()
        {
            if (Model.SelectPreviousInspectorBuff())
                RefreshSelectedInspectorData();
        }

        private void HandleNextInspectorBuffRequested()
        {
            if (Model.SelectNextInspectorBuff())
                RefreshSelectedInspectorData();
        }

        private void HandleUnitInspectorApplyRequested(string text)
        {
            if (!TryGetInspectorContext(out EntityManager entityManager, out Entity entity, out UnitDebugComponentEntry component))
                return;

            UnitDebugInspectorUtility.ApplyEditorText(
                entityManager,
                entity,
                component.Kind,
                Model.SelectedInspectorBuffIndex,
                text,
                out string message);
            RefreshSelectedInspectorData(message);
        }

        private void HandleUnitInspectorAddBuffRequested(string text)
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            UnitDebugInspectorUtility.AddBuff(entityManager, entity, text, out string message);
            RefreshSelectedInspectorData(message);
        }

        private void HandleUnitInspectorRemoveBuffRequested()
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            UnitDebugInspectorUtility.RemoveSelectedBuff(entityManager, entity, Model.SelectedInspectorBuffIndex, out string message);
            RefreshSelectedInspectorData(message);
        }

        private void HandleUnitInspectorClearBehaviorRequested()
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            bool cleared = UnitDebugInspectorUtility.ClearBehaviorTree(entityManager, entity);
            RefreshSelectedInspectorData(cleared ? "Behavior tree cleared." : "No behavior tree is attached.");
        }

        private void HandleUnitInspectorClearStateScriptRequested()
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            bool cleared = UnitDebugInspectorUtility.ClearStateScript(entityManager, entity);
            RefreshSelectedInspectorData(cleared ? "State script cleared." : "No state script is attached.");
        }

        private void RefreshUnitInspector()
        {
            if (!DebugComponent.Instance.IsEnabled || !Model.IsTrainingGroundActive)
            {
                Model.SetUnitInspectorDetail("Unit Inspector is available only while Debug is enabled in the training ground.");
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Model.SetUnitInspectorDetail("Runtime world is not available.");
                return;
            }

            UnitDebugInspectorUtility.GetUnits(world.EntityManager, _inspectorUnits);
            Model.SetInspectorUnits(_inspectorUnits);
            RefreshSelectedInspectorData();
        }

        private void RefreshSelectedInspectorData(string status = "")
        {
            if (!TryGetInspectorEntity(out EntityManager entityManager, out Entity entity))
                return;

            UnitDebugInspectorUtility.GetComponents(entityManager, entity, _inspectorComponents);
            Model.SetInspectorComponents(_inspectorComponents);
            UnitDebugInspectorUtility.GetBuffs(entityManager, entity, _inspectorBuffs);
            Model.SetInspectorBuffs(_inspectorBuffs);

            if (!Model.TryGetSelectedInspectorComponent(out UnitDebugComponentEntry component))
            {
                Model.SetUnitInspectorDetail("No supported Component is attached to this unit.", status);
                return;
            }

            Model.SetUnitInspectorDetail(
                UnitDebugInspectorUtility.BuildEditorText(
                    entityManager,
                    entity,
                    component.Kind,
                    Model.SelectedInspectorBuffIndex),
                status);
        }

        private bool TryGetInspectorContext(
            out EntityManager entityManager,
            out Entity entity,
            out UnitDebugComponentEntry component)
        {
            if (TryGetInspectorEntity(out entityManager, out entity) && Model.TryGetSelectedInspectorComponent(out component))
                return true;

            component = default;
            return false;
        }

        private bool TryGetInspectorEntity(out EntityManager entityManager, out Entity entity)
        {
            entityManager = default;
            entity = Entity.Null;
            if (!DebugComponent.Instance.IsEnabled ||
                !Model.IsTrainingGroundActive ||
                !Model.TryGetSelectedInspectorUnit(out UnitDebugUnitEntry unit))
            {
                return false;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || !world.EntityManager.Exists(unit.Entity))
                return false;

            entityManager = world.EntityManager;
            entity = unit.Entity;
            return true;
        }
    }
}
