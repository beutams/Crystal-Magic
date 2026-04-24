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
        bool simulationLocked = GameGateComponent.Instance != null && GameGateComponent.Instance.IsSimulationLocked;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (intent, perception, behaviorTree, entity) in
                 SystemAPI.Query<RefRW<UnitIntentComponent>, RefRO<UnitPerceptionComponent>, UnitBehaviorTreeComponent>()
                     .WithAll<UnitAITag>()
                     .WithEntityAccess())
        {
            if (simulationLocked || behaviorTree == null || !behaviorTree.IsEnabled || !behaviorTree.IsInitialized || behaviorTree.Runtime == null)
            {
                intent.ValueRW.MoveDirection = float2.zero;
                intent.ValueRW.WantToCast = false;
                intent.ValueRW.HasCastTarget = false;
                intent.ValueRW.CastTargetPosition = float2.zero;
                if (behaviorTree != null)
                {
                    behaviorTree.CurrentNodeName = "None";
                    behaviorTree.LastStatus = "None";
                }
                continue;
            }

            if (behaviorTree.TickInterval > 0f)
            {
                behaviorTree.TimeUntilNextTick -= deltaTime;
                if (behaviorTree.TimeUntilNextTick > 0f)
                {
                    behaviorTree.CurrentNodeName = behaviorTree.Blackboard?.CurrentNodeName ?? "None";
                    behaviorTree.LastStatus = behaviorTree.Blackboard?.LastStatus ?? "None";
                    continue;
                }
            }

            intent.ValueRW.MoveDirection = float2.zero;
            intent.ValueRW.WantToCast = false;
            intent.ValueRW.HasCastTarget = false;
            intent.ValueRW.CastTargetPosition = float2.zero;

            _context.BeginTick(entity, EntityManager, perception.ValueRO, intent.ValueRO, behaviorTree.Blackboard);
            BehaviorNodeStatus status = behaviorTree.Runtime.Tick(_context);
            intent.ValueRW = _context.Intent;

            behaviorTree.CurrentNodeName = behaviorTree.Blackboard?.CurrentNodeName ?? "None";
            behaviorTree.LastStatus = status.ToString();
            if (behaviorTree.TickInterval > 0f)
                behaviorTree.TimeUntilNextTick = behaviorTree.TickInterval;
        }
    }
}
