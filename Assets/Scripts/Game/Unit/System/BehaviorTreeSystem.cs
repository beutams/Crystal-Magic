using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

[UpdateAfter(typeof(UnitPerceptionSystem))]
[UpdateBefore(typeof(UnitStateMachineSystem))]
partial class BehaviorTreeSystem : SystemBase
{
    private readonly BehaviorTreeContext _context = new();

    protected override void OnUpdate()
    {
        bool simulationLocked =
            GameGateComponent.TryGetInstance(out GameGateComponent gameGateComponent) &&
            gameGateComponent.IsSimulationLocked;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (intent, perception, behaviorTree, entity) in
                 SystemAPI.Query<RefRW<UnitIntentComponent>, RefRO<UnitPerceptionComponent>, UnitBehaviorTreeComponent>()
                     .WithEntityAccess())
        {
            if (simulationLocked || behaviorTree == null || !behaviorTree.IsInitialized || behaviorTree.Runtime == null)
            {
                intent.ValueRW.MoveDirection = float2.zero;
                intent.ValueRW.WantToCast = false;
                intent.ValueRW.CastTargetPosition = float2.zero;
                if (behaviorTree != null)
                {
                    behaviorTree.CurrentNodeName = "None";
                    behaviorTree.LastStatus = "None";
                }
                continue;
            }

            intent.ValueRW.MoveDirection = float2.zero;
            intent.ValueRW.WantToCast = false;
            intent.ValueRW.CastTargetPosition = float2.zero;

            _context.BeginTick(entity, EntityManager, deltaTime, perception.ValueRO, intent.ValueRO, behaviorTree.Blackboard);
            BehaviorNodeStatus status = behaviorTree.Runtime.Tick(_context);
            intent.ValueRW = _context.Intent;

            behaviorTree.CurrentNodeName = behaviorTree.Blackboard?.CurrentNodeName ?? "None";
            behaviorTree.LastStatus = status.ToString();
        }
    }
}
