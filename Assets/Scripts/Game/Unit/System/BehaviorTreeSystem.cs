using CrystalMagic.Core;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitPerceptionSystem))]
partial class BehaviorTreeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (GameGateComponent.Instance.IsSimulationLocked)
            return;
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = EntityManager;

        foreach (var (behaviorTree, entity) in
                 SystemAPI.Query<UnitBehaviorTreeComponent>()
                     .WithEntityAccess())
        {
            if (behaviorTree == null)
                continue;

            behaviorTree.Blackboard ??= new BehaviorBlackboard();
            SyncBlackboard(entityManager, entity, deltaTime, behaviorTree.Blackboard);

            if (behaviorTree.IsInitialized && behaviorTree.Runtime != null)
                behaviorTree.Runtime.Tick(behaviorTree.Blackboard);

            ApplyBlackboardCommands(entityManager, entity, behaviorTree.Blackboard);
            behaviorTree.CurrentNodeName = behaviorTree.Blackboard.Debug.CurrentNodeName ?? "None";
            behaviorTree.LastStatus = behaviorTree.Blackboard.Debug.LastStatus ?? "None";
        }
    }

    private static void SyncBlackboard(
        EntityManager entityManager,
        Entity entity,
        float deltaTime,
        BehaviorBlackboard blackboard)
    {
        blackboard.ResetFrame();
        blackboard.Runtime.Entity = entity;
        blackboard.Runtime.EntityManager = entityManager;
        blackboard.Runtime.DeltaTime = deltaTime;

        if (entity != Entity.Null &&
            entityManager.Exists(entity) &&
            entityManager.HasComponent<LocalTransform>(entity))
        {
            blackboard.Sense.HasSelfPosition = true;
            blackboard.Sense.SelfPosition = entityManager.GetComponentData<LocalTransform>(entity).Position.xy;
        }

        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitPerceptionComponent>(entity))
        {
            return;
        }

        UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
        blackboard.Sense.HasTarget = perception.HasTarget;
        blackboard.Sense.TargetEntity = perception.TargetEntity;
        blackboard.Sense.TargetPosition = perception.TargetPosition;
        blackboard.Sense.TargetDistance = perception.TargetDistance;
    }

    private static void ApplyBlackboardCommands(
        EntityManager entityManager,
        Entity entity,
        BehaviorBlackboard blackboard)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return;

        if (entityManager.HasComponent<UnitIntentComponent>(entity))
        {
            UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
            intent.ClearFrameIntent();
            intent.MoveDirection = blackboard.Intent.MoveDirection;
            intent.WantToCast = blackboard.Intent.WantToCast;
            intent.CastTargetPosition = blackboard.Intent.CastTargetPosition;
            intent.SkillRequestMode = blackboard.Intent.SkillRequestMode;
            intent.RequestedSkillId = blackboard.Intent.RequestedSkillId;
            intent.RequestedTagMask = blackboard.Intent.RequestedTagMask;
            entityManager.SetComponentData(entity, intent);
        }
    }
}
