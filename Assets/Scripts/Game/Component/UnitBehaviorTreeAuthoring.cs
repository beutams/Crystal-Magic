using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitBehaviorTreeAuthoring : MonoBehaviour
{
    class UnitBehaviorTreeBaker : Baker<UnitBehaviorTreeAuthoring>
    {
        public override void Bake(UnitBehaviorTreeAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            UnitData unitData = UnitAuthoringUtility.ResolveUnitData(authoring);
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitBehaviorTreeComponent
            {
                UnitDataId = unitData?.Id ?? -1,
                TreeIndex = -1,
                CurrentNodeIndex = -1,
            });
            AddBuffer<BehaviorNodeStateElement>(entity);
            AddBuffer<BehaviorTreeCommandElement>(entity);
            AddBuffer<BehaviorTreeCommandArgumentElement>(entity);
            AddBuffer<BehaviorTreeMoveCommandElement>(entity);
            AddBuffer<BehaviorTreeHitDebugElement>(entity);
        }
    }
}

public struct UnitBehaviorTreeComponent : IComponentData
{
    public int UnitDataId;
    public int TreeIndex;
    public int CurrentNodeIndex;
    public BehaviorNodeStatus LastStatus;
    public uint TickVersion;
    public BehaviorTreeInitializationError InitializationError;
}
