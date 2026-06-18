using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
partial class BehaviorTreeInitSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (UnitBehaviorTreeComponent behaviorTree in
                 SystemAPI.Query<UnitBehaviorTreeComponent>())
        {
            if (behaviorTree == null || behaviorTree.IsInitialized)
                continue;

            behaviorTree.Blackboard = new BehaviorBlackboard();
            behaviorTree.Runtime = null;
            behaviorTree.CurrentNodeName = "None";
            behaviorTree.LastStatus = "None";

            if (string.IsNullOrWhiteSpace(behaviorTree.UnitName))
            {
                behaviorTree.IsInitialized = true;
                continue;
            }

            BehaviorTreeData data = DataComponent.Instance.Find<BehaviorTreeData>(
                row => string.Equals(row.Name, behaviorTree.UnitName, System.StringComparison.Ordinal));
            if (data == null)
            {
                Debug.LogWarning($"[BehaviorTreeInit] BehaviorTreeData not found for unit: {behaviorTree.UnitName}");
                behaviorTree.IsInitialized = true;
                continue;
            }

            behaviorTree.Runtime = BehaviorTreeBuilder.Build(data);
            behaviorTree.IsInitialized = true;
        }
    }
}
