using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateBefore(typeof(BehaviorTreeSystem))]
partial class BehaviorTreeInitSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (UnitBehaviorTreeComponent behaviorTree in
                 SystemAPI.Query<UnitBehaviorTreeComponent>().WithAll<UnitAITag>())
        {
            if (behaviorTree == null || behaviorTree.IsInitialized)
                continue;

            behaviorTree.Blackboard = new BehaviorBlackboard();
            behaviorTree.Runtime = null;
            behaviorTree.CurrentNodeName = "None";
            behaviorTree.LastStatus = "None";

            if (behaviorTree.BehaviorTreeId <= 0)
            {
                behaviorTree.IsInitialized = true;
                continue;
            }

            BehaviorTreeData data = DataComponent.Instance?.Get<BehaviorTreeData>(behaviorTree.BehaviorTreeId);
            if (data == null)
            {
                Debug.LogWarning($"[BehaviorTreeInit] BehaviorTreeData not found: {behaviorTree.BehaviorTreeId}");
                behaviorTree.IsInitialized = true;
                continue;
            }

            behaviorTree.Runtime = BehaviorTreeBuilder.Build(data);
            behaviorTree.IsInitialized = true;
        }
    }
}
