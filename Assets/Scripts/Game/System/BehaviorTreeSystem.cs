using CrystalMagic.Core;
using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitPerceptionSystem))]
partial class BehaviorTreeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        GameGateComponent gameGate = GameGateComponent.Instance;
        if (gameGate != null && gameGate.IsSimulationLocked)
            return;

        bool isDebugEnabled = DebugComponent.Instance.IsEnabled;
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = EntityManager;

        foreach (var (behaviorTree, entity) in
                 SystemAPI.Query<UnitBehaviorTreeComponent>()
                     .WithNone<UnitDeathComponent>()
                     .WithEntityAccess())
        {
            if (behaviorTree == null)
                continue;

            if (!entityManager.HasComponent<UnitSourceRuntimeComponent>(entity))
                continue;

            UnitSourceRuntimeComponent sourceRuntime = entityManager.GetComponentObject<UnitSourceRuntimeComponent>(entity);
            if (sourceRuntime?.Table == null)
                continue;

            behaviorTree.Context ??= new BehaviorContext();
            behaviorTree.Context.BeginFrame(entity, entityManager, deltaTime, sourceRuntime.Table, isDebugEnabled);

            if (behaviorTree.IsInitialized && behaviorTree.Runtime != null)
                behaviorTree.Runtime.Tick(behaviorTree.Context);

            if (isDebugEnabled)
            {
                behaviorTree.CurrentNodeName = behaviorTree.Context.Debug.CurrentNodeName ?? "None";
                behaviorTree.LastStatus = behaviorTree.Context.Debug.LastStatus ?? "None";
            }
        }
    }
}
