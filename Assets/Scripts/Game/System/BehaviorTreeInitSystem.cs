using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitSourceDispatcherSystem))]
public partial class BehaviorTreeInitSystem : SystemBase
{
    private BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> _registry;
    private EntityQuery _behaviorTreeQuery;
    private bool _compileFailed;

    protected override void OnCreate()
    {
        _behaviorTreeQuery = GetEntityQuery(ComponentType.ReadWrite<UnitBehaviorTreeComponent>());
    }

    protected override void OnUpdate()
    {
        if (!_registry.IsCreated && !_compileFailed)
            TryCompileRegistry();
        if (!_registry.IsCreated)
            return;

        using NativeArray<Entity> entities = _behaviorTreeQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities[entityIndex];
            UnitBehaviorTreeComponent component = EntityManager.GetComponentData<UnitBehaviorTreeComponent>(entity);
            if (component.IsInitialized != 0)
                continue;

            component.TreeIndex = -1;
            component.CurrentNodeIndex = -1;
            component.LastStatus = BehaviorNodeStatus.Failure;
            component.TickVersion = 0;
            component.InitializationError = BehaviorTreeInitializationError.None;

            if (component.UnitDataId < 0)
            {
                component.InitializationError = BehaviorTreeInitializationError.MissingUnitDataId;
            }
            else
            {
                component.TreeIndex = BehaviorTreeCompiler.FindTreeIndex(in _registry, component.UnitDataId);
                if (component.TreeIndex < 0)
                {
                    component.InitializationError = BehaviorTreeInitializationError.TreeNotFound;
                    Debug.LogWarning($"[BehaviorTreeInit] BehaviorTreeData not found for UnitDataId: {component.UnitDataId}");
                }
                else
                {
                    ref BehaviorTreeDefinitionBlob tree = ref _registry.Value.Trees[component.TreeIndex];
                    DynamicBuffer<BehaviorNodeStateElement> states =
                        EntityManager.GetBuffer<BehaviorNodeStateElement>(entity);
                    states.ResizeUninitialized(tree.Nodes.Length);
                    for (int nodeIndex = 0; nodeIndex < states.Length; nodeIndex++)
                        states[nodeIndex] = BehaviorNodeStateElement.CreateDefault();
                }
            }

            EntityManager.GetBuffer<BehaviorTreeCommandElement>(entity).Clear();
            EntityManager.GetBuffer<BehaviorTreeCommandArgumentElement>(entity).Clear();
            EntityManager.GetBuffer<BehaviorTreeMoveCommandElement>(entity).Clear();
            EntityManager.GetBuffer<BehaviorTreeHitDebugElement>(entity).Clear();
            component.IsInitialized = 1;
            EntityManager.SetComponentData(entity, component);
        }
    }

    protected override void OnDestroy()
    {
        if (_registry.IsCreated)
            _registry.Dispose();
    }

    private void TryCompileRegistry()
    {
        DataTable<BehaviorTreeData> table = DataComponent.Instance.GetTable<BehaviorTreeData>();
        if (table == null)
            return;

        List<BehaviorTreeData> trees = new(table.GetAll());
        if (!BehaviorTreeCompiler.TryBuildRegistry(trees, out _registry, out string error))
        {
            _compileFailed = true;
            Debug.LogError($"[BehaviorTreeInit] Failed to compile behavior trees: {error}");
            return;
        }

        Entity registryEntity = EntityManager.CreateEntity(typeof(BehaviorTreeRuntimeRegistryComponent));
        EntityManager.SetName(registryEntity, "BehaviorTreeRuntimeRegistry");
        EntityManager.SetComponentData(registryEntity, new BehaviorTreeRuntimeRegistryComponent
        {
            Value = _registry,
        });
    }
}
