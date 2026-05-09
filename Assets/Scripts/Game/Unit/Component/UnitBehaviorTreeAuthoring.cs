using Unity.Entities;
using UnityEngine;

public class UnitBehaviorTreeAuthoring : MonoBehaviour
{
    class UnitBehaviorTreeBaker : Baker<UnitBehaviorTreeAuthoring>
    {
        public override void Bake(UnitBehaviorTreeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitBehaviorTreeComponent
            {
                UnitName = authoring.transform.root != null
                    ? authoring.transform.root.name
                    : authoring.gameObject.name,
            });
        }
    }
}

public class UnitBehaviorTreeComponent : IComponentData
{
    public string UnitName;
    public bool IsInitialized;
    public string CurrentNodeName = "None";
    public string LastStatus = "None";
    [System.NonSerialized] public BehaviorTreeRuntime Runtime;
    [System.NonSerialized] public BehaviorBlackboard Blackboard;
}
