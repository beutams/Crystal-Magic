using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

public sealed class BehaviorBlackboard
{
    public BehaviorBlackboardRuntime Runtime;
    public BehaviorBlackboardSense Sense;
    public BehaviorBlackboardIntent Intent;
    public BehaviorBlackboardDebug Debug;

    public void ResetFrame()
    {
        Runtime.Entity = Entity.Null;
        Runtime.EntityManager = default;
        Runtime.DeltaTime = 0f;

        Sense.HasSelfPosition = false;
        Sense.SelfPosition = float2.zero;
        Sense.HasTarget = false;
        Sense.TargetEntity = Entity.Null;
        Sense.TargetPosition = float2.zero;
        Sense.TargetDistance = 0f;

        Intent.MoveDirection = float2.zero;
        Intent.WantToCast = false;
        Intent.CastTargetPosition = float2.zero;
        Intent.SkillRequestMode = UnitSkillSelectionMode.None;
        Intent.RequestedSkillId = -1;
        Intent.RequestedTagMask = 0;

        Debug.CurrentNodeName = "None";
        Debug.LastStatus = "None";
    }

    public void SetCurrentNode(ABehaviorNode node)
    {
        if (node != null)
            Debug.CurrentNodeName = node.DisplayName;
    }

    public void SetMoveDirection(float2 direction)
    {
        Intent.MoveDirection = direction;
    }

    public void SetCastTarget(float2 position)
    {
        Intent.CastTargetPosition = position;
    }

    public void SetWantToCast()
    {
        Intent.WantToCast = true;
    }

    public void SetSkillRequest(UnitSkillSelectionMode requestMode, int requestedSkillId, int requestedTagMask)
    {
        Intent.SkillRequestMode = requestMode;
        Intent.RequestedSkillId = requestedSkillId;
        Intent.RequestedTagMask = requestedTagMask;
    }

    public bool TryGetSelfPosition(out float2 position)
    {
        if (Sense.HasSelfPosition)
        {
            position = Sense.SelfPosition;
            return true;
        }

        position = float2.zero;
        return false;
    }

    public bool TryGetTargetPosition(out float2 position)
    {
        if (Sense.HasTarget)
        {
            position = Sense.TargetPosition;
            return true;
        }

        position = float2.zero;
        return false;
    }

    public bool TryGetTargetEntity(out Entity targetEntity)
    {
        if (Sense.HasTarget)
        {
            targetEntity = Sense.TargetEntity;
            return true;
        }

        targetEntity = Entity.Null;
        return false;
    }
}

public struct BehaviorBlackboardRuntime
{
    public Entity Entity;
    public EntityManager EntityManager;
    public float DeltaTime;
}

public struct BehaviorBlackboardSense
{
    public bool HasSelfPosition;
    public float2 SelfPosition;
    public bool HasTarget;
    public Entity TargetEntity;
    public float2 TargetPosition;
    public float TargetDistance;
}

public struct BehaviorBlackboardIntent
{
    public float2 MoveDirection;
    public bool WantToCast;
    public float2 CastTargetPosition;
    public UnitSkillSelectionMode SkillRequestMode;
    public int RequestedSkillId;
    public int RequestedTagMask;
}

public struct BehaviorBlackboardDebug
{
    public string CurrentNodeName;
    public string LastStatus;
}

public sealed class BehaviorTreeRuntime
{
    private readonly ABehaviorNode _root;

    public BehaviorTreeRuntime(ABehaviorNode root)
    {
        _root = root;
    }

    public bool IsValid => _root != null;

    public BehaviorNodeStatus Tick(BehaviorBlackboard blackboard)
    {
        if (_root == null)
            return BehaviorNodeStatus.Failure;

        BehaviorNodeStatus status = _root.Tick(blackboard);
        if (blackboard != null)
            blackboard.Debug.LastStatus = status.ToString();
        return status;
    }

    public void Reset()
    {
        _root?.Reset();
    }
}

public static class BehaviorTreeBuilder
{
    private static BehaviorNodeFactory s_factory;

    public static BehaviorTreeRuntime Build(BehaviorTreeData data)
    {
        if (data == null || data.Nodes == null || data.Nodes.Count == 0)
            return null;

        BehaviorNodeFactory factory = GetFactory();

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

    private static BehaviorNodeFactory GetFactory()
    {
        if (s_factory != null)
            return s_factory;

        s_factory = new BehaviorNodeFactory();
        BehaviorTreeRegistry.RegisterAll(s_factory);
        return s_factory;
    }
}
