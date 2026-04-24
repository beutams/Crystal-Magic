using Unity.Entities;
using UnityEngine;

public class UnitBehaviorTreeAuthoring : MonoBehaviour
{
    [SerializeField] private int _behaviorTreeId;
    [SerializeField] private float _tickInterval = 0f;
    [SerializeField] private bool _enableOnStart = true;

    public int BehaviorTreeId
    {
        get => _behaviorTreeId;
        set => _behaviorTreeId = value;
    }

    public float TickInterval
    {
        get => _tickInterval;
        set => _tickInterval = Mathf.Max(0f, value);
    }

    public bool EnableOnStart
    {
        get => _enableOnStart;
        set => _enableOnStart = value;
    }

    class UnitBehaviorTreeBaker : Baker<UnitBehaviorTreeAuthoring>
    {
        public override void Bake(UnitBehaviorTreeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitBehaviorTreeComponent
            {
                BehaviorTreeId = authoring.BehaviorTreeId,
                TickInterval = Mathf.Max(0f, authoring.TickInterval),
                IsEnabled = authoring.EnableOnStart,
                TimeUntilNextTick = 0f,
            });
        }
    }
}

public class UnitBehaviorTreeComponent : IComponentData
{
    public int BehaviorTreeId;
    public float TickInterval;
    public bool IsEnabled = true;
    public bool IsInitialized;
    public float TimeUntilNextTick;
    public string CurrentNodeName = "None";
    public string LastStatus = "None";
    [System.NonSerialized] public BehaviorTreeRuntime Runtime;
    [System.NonSerialized] public BehaviorBlackboard Blackboard;
}
