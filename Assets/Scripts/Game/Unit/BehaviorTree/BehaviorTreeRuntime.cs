using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class BehaviorBlackboard
{
    public Entity CurrentTargetEntity = Entity.Null;
    public float2 CurrentTargetPosition;
    public string CurrentNodeName = "None";
    public string LastStatus = "None";

    public void ResetFrame()
    {
        CurrentNodeName = "None";
        LastStatus = "None";
    }
}

public sealed class BehaviorTreeContext
{
    public Entity Entity;
    public EntityManager EntityManager;
    public UnitPerceptionComponent Perception;
    public UnitIntentComponent Intent;
    public BehaviorBlackboard Blackboard;

    public void BeginTick(Entity entity, EntityManager entityManager, in UnitPerceptionComponent perception, in UnitIntentComponent intent, BehaviorBlackboard blackboard)
    {
        Entity = entity;
        EntityManager = entityManager;
        Perception = perception;
        Intent = intent;
        Blackboard = blackboard;
        Blackboard?.ResetFrame();
    }

    public void SetMoveDirection(float2 direction)
    {
        Intent.MoveDirection = direction;
    }

    public void SetCastTarget(float2 position)
    {
        Intent.HasCastTarget = true;
        Intent.CastTargetPosition = position;
    }

    public void SetWantToCast()
    {
        Intent.WantToCast = true;
    }

    public void MarkCurrentNode(ABehaviorNode node)
    {
        if (Blackboard != null && node != null)
            Blackboard.CurrentNodeName = node.DisplayName;
    }

    public bool TryGetSelfPosition(out float2 position)
    {
        if (Entity != Entity.Null &&
            EntityManager.Exists(Entity) &&
            EntityManager.HasComponent<LocalTransform>(Entity))
        {
            float3 entityPosition = EntityManager.GetComponentData<LocalTransform>(Entity).Position;
            position = entityPosition.xy;
            return true;
        }

        position = float2.zero;
        return false;
    }

    public bool TryGetTargetPosition(out float2 position)
    {
        if (Perception.HasTarget)
        {
            position = Perception.TargetPosition;
            return true;
        }

        if (Blackboard != null && Blackboard.CurrentTargetEntity != Entity.Null)
        {
            position = Blackboard.CurrentTargetPosition;
            return true;
        }

        position = float2.zero;
        return false;
    }

    public bool TryGetTargetEntity(out Entity targetEntity)
    {
        if (Perception.HasTarget)
        {
            targetEntity = Perception.TargetEntity;
            return true;
        }

        if (Blackboard != null && Blackboard.CurrentTargetEntity != Entity.Null)
        {
            targetEntity = Blackboard.CurrentTargetEntity;
            return true;
        }

        targetEntity = Entity.Null;
        return false;
    }

    public bool TryGetCastRange(out float castRange)
    {
        if (Entity != Entity.Null &&
            EntityManager.Exists(Entity) &&
            EntityManager.HasComponent<UnitAttackComponent>(Entity))
        {
            castRange = EntityManager.GetComponentData<UnitAttackComponent>(Entity).RealSkillRange;
            return true;
        }

        castRange = 0f;
        return false;
    }

    public void SyncBlackboardTarget()
    {
        if (Blackboard == null)
            return;

        Blackboard.CurrentTargetEntity = Perception.HasTarget ? Perception.TargetEntity : Entity.Null;
        Blackboard.CurrentTargetPosition = Perception.HasTarget ? Perception.TargetPosition : float2.zero;
    }
}

public sealed class BehaviorTreeRuntime
{
    private readonly ABehaviorNode _root;

    public BehaviorTreeRuntime(ABehaviorNode root)
    {
        _root = root;
    }

    public bool IsValid => _root != null;

    public BehaviorNodeStatus Tick(BehaviorTreeContext context)
    {
        if (_root == null)
            return BehaviorNodeStatus.Failure;

        BehaviorNodeStatus status = _root.Tick(context);
        if (context.Blackboard != null)
            context.Blackboard.LastStatus = status.ToString();
        return status;
    }

    public void Reset()
    {
        _root?.Reset();
    }
}

public static class BehaviorTreeBuilder
{
    public static BehaviorTreeRuntime Build(BehaviorTreeData data)
    {
        if (data == null || data.Nodes == null || data.Nodes.Count == 0)
            return null;

        var factory = new BehaviorNodeFactory();
        BehaviorTreeRegistry.RegisterAll(factory);

        var runtimeNodes = new Dictionary<string, ABehaviorNode>(System.StringComparer.Ordinal);
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            BehaviorNodeData nodeData = data.Nodes[i];
            if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid))
                continue;

            ABehaviorNode node = factory.CreateNode(nodeData);
            if (node != null)
                runtimeNodes[nodeData.Guid] = node;
        }

        for (int i = 0; i < data.Nodes.Count; i++)
        {
            BehaviorNodeData nodeData = data.Nodes[i];
            if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid))
                continue;
            if (!runtimeNodes.TryGetValue(nodeData.Guid, out ABehaviorNode node))
                continue;

            nodeData.ChildGuids ??= new List<string>();
            for (int childIndex = 0; childIndex < nodeData.ChildGuids.Count; childIndex++)
            {
                string childGuid = nodeData.ChildGuids[childIndex];
                if (string.IsNullOrWhiteSpace(childGuid))
                    continue;

                if (runtimeNodes.TryGetValue(childGuid, out ABehaviorNode child))
                    node.AddChild(child);
            }
        }

        if (string.IsNullOrWhiteSpace(data.RootNodeGuid) ||
            !runtimeNodes.TryGetValue(data.RootNodeGuid, out ABehaviorNode root))
        {
            return null;
        }

        return new BehaviorTreeRuntime(root);
    }
}
