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
            AddComponentObject(entity, new UnitBehaviorTreeComponent
            {
                UnitDataId = unitData?.Id ?? -1,
            });
        }
    }
}

public class UnitBehaviorTreeComponent : IComponentData
{
    public int UnitDataId;
    public bool IsInitialized;
    public string CurrentNodeName = "None";
    public string LastStatus = "None";
    public string InitializationError = string.Empty;
    [System.NonSerialized] public BehaviorTreeRuntime Runtime;
    [System.NonSerialized] public BehaviorContext Context;
}
