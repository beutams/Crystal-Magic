using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitSourceDispatcherSystem))]
public partial class StateScriptInitSystem : SystemBase
{
    private BlobAssetReference<StateScriptRuntimeRegistryBlob> _registry;
    private EntityQuery _stateScriptQuery;

    protected override void OnCreate()
    {
        _stateScriptQuery = GetEntityQuery(
            ComponentType.ReadWrite<UnitStateScriptComponent>(),
            ComponentType.ReadOnly<UnitInitializationPendingTag>());

        DataTable<StateScriptData> table = DataComponent.Instance.GetTable<StateScriptData>();
        if (table == null)
        {
            Debug.LogError("[StateScriptInit] StateScriptData table is not loaded.");
            Enabled = false;
            return;
        }

        List<StateScriptData> rows = new(table.GetAll());
        if (!StateScriptCompiler.TryBuildRegistry(rows, out _registry, out string error))
        {
            Debug.LogError($"[StateScriptInit] Failed to compile state scripts: {error}");
            Enabled = false;
            return;
        }

        Entity registryEntity = EntityManager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent));
        EntityManager.SetName(registryEntity, "StateScriptRuntimeRegistry");
        EntityManager.SetComponentData(registryEntity, new StateScriptRuntimeRegistryComponent
        {
            Value = _registry,
        });
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> entities = _stateScriptQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities[entityIndex];
            UnitStateScriptComponent component = EntityManager.GetComponentData<UnitStateScriptComponent>(entity);
            component.DefinitionIndex = -1;
            component.TickVersion = 0;
            component.InitializationError = StateScriptInitializationError.None;
            component.IsStoppedForDeath = 0;
            if (component.UnitDataId < 0)
            {
                component.InitializationError = StateScriptInitializationError.MissingUnitDataId;
            }
            else
            {
                component.DefinitionIndex = StateScriptCompiler.FindUnitIndex(in _registry, component.UnitDataId);
                if (component.DefinitionIndex < 0)
                {
                    component.InitializationError = StateScriptInitializationError.DefinitionNotFound;
                    Debug.LogWarning($"[StateScriptInit] StateScriptData not found for UnitDataId: {component.UnitDataId}");
                }
                else
                {
                    InitializeStateBuffers(entity, ref _registry.Value.Units[component.DefinitionIndex]);
                }
            }

            EntityManager.GetBuffer<StateScriptSourceCommandElement>(entity).Clear();
            EntityManager.GetBuffer<StateScriptSourceCommandArgumentElement>(entity).Clear();
            EntityManager.GetBuffer<StateScriptManagedCommandElement>(entity).Clear();
            EntityManager.GetBuffer<StateScriptExternalResultElement>(entity).Clear();
            EntityManager.SetComponentData(entity, component);
        }
    }

    protected override void OnDestroy()
    {
        if (_registry.IsCreated)
            _registry.Dispose();
    }

    private void InitializeStateBuffers(Entity entity, ref StateScriptUnitDefinitionBlob definition)
    {
        DynamicBuffer<StateScriptGraphStateElement> graphStates =
            EntityManager.GetBuffer<StateScriptGraphStateElement>(entity);
        DynamicBuffer<StateScriptNodeStateElement> nodeStates =
            EntityManager.GetBuffer<StateScriptNodeStateElement>(entity);
        graphStates.ResizeUninitialized(definition.Graphs.Length);
        int stateStart = 0;
        for (int graphIndex = 0; graphIndex < definition.Graphs.Length; graphIndex++)
        {
            ref StateScriptGraphDefinitionBlob graph = ref definition.Graphs[graphIndex];
            graphStates[graphIndex] = new StateScriptGraphStateElement
            {
                NodeStateStart = stateStart,
            };
            stateStart += graph.Nodes.Length;
        }

        nodeStates.ResizeUninitialized(stateStart);
        for (int index = 0; index < nodeStates.Length; index++)
            nodeStates[index] = default;
    }

}
