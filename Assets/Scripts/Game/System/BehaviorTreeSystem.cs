using CrystalMagic.Core;
using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitPerceptionSystem))]
partial class BehaviorTreeSystem : SystemBase
{
    private UnitSourceDispatcher _sourceDispatcher;

    protected override void OnCreate()
    {
        _sourceDispatcher.Initialize(this);
    }

    protected override void OnUpdate()
    {
        GameGateComponent gameGate = GameGateComponent.Instance;
        if (gameGate != null && gameGate.IsSimulationLocked)
            return;

        bool isDebugEnabled = DebugComponent.Instance.IsEnabled;
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = EntityManager;
        _sourceDispatcher.Update(this);

        foreach (var (behaviorTree, entity) in
                 SystemAPI.Query<UnitBehaviorTreeComponent>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            if (behaviorTree == null)
                continue;

            if (!behaviorTree.IsInitialized || behaviorTree.Runtime?.Sources == null)
                continue;

            behaviorTree.Runtime.Sources.Update(entity, entityManager, in _sourceDispatcher);
            behaviorTree.Context ??= new BehaviorContext();
            behaviorTree.Context.BeginFrame(
                entity,
                entityManager,
                deltaTime,
                behaviorTree.Runtime.Sources,
                isDebugEnabled);

            behaviorTree.Runtime.Tick(behaviorTree.Context);

            if (isDebugEnabled)
            {
                behaviorTree.CurrentNodeName = behaviorTree.Context.Debug.CurrentNodeName ?? "None";
                behaviorTree.LastStatus = behaviorTree.Context.Debug.LastStatus ?? "None";
            }
        }
    }
}
