using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
partial class BehaviorTreeInitSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((UnitBehaviorTreeComponent behaviorTree, Entity entity) in
                 SystemAPI.Query<UnitBehaviorTreeComponent>().WithEntityAccess())
        {
            if (behaviorTree == null || behaviorTree.IsInitialized)
                continue;

            behaviorTree.Context = new BehaviorContext();
            behaviorTree.Runtime = null;
            behaviorTree.CurrentNodeName = "None";
            behaviorTree.LastStatus = "None";
            behaviorTree.InitializationError = string.Empty;

            if (!EntityManager.HasComponent<UnitSourceRuntimeComponent>(entity))
                continue;

            if (behaviorTree.UnitDataId < 0)
            {
                behaviorTree.InitializationError = "Unit has no UnitDataId for behavior tree binding.";
                behaviorTree.IsInitialized = true;
                continue;
            }

            BehaviorTreeData data = DataComponent.Instance.Find<BehaviorTreeData>(
                row => row.UnitDataId == behaviorTree.UnitDataId);
            if (data == null)
            {
                behaviorTree.InitializationError = $"BehaviorTreeData not found for UnitDataId: {behaviorTree.UnitDataId}";
                Debug.LogWarning($"[BehaviorTreeInit] {behaviorTree.InitializationError}");
                behaviorTree.IsInitialized = true;
                continue;
            }

            UnitSourceRuntimeComponent sourceRuntime = EntityManager.GetComponentObject<UnitSourceRuntimeComponent>(entity);
            behaviorTree.Runtime = BehaviorTreeBuilder.Build(data, sourceRuntime?.Table, out string error);
            if (behaviorTree.Runtime == null)
            {
                behaviorTree.InitializationError = error;
                Debug.LogWarning($"[BehaviorTreeInit] Failed to bind UnitDataId {behaviorTree.UnitDataId}: {error}");
            }
            behaviorTree.IsInitialized = true;
        }
    }
}
