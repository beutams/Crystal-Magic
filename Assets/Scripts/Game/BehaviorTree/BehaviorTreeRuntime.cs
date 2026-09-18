using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;

// Per-unit execution context. It never mirrors unit component data.
public sealed class BehaviorContext
{
    public Entity Entity { get; private set; }
    public EntityManager EntityManager { get; private set; }
    public float DeltaTime { get; private set; }
    public UnitSourceResolver Sources { get; private set; }
    public BehaviorDebugState Debug { get; } = new();

    public void BeginFrame(
        Entity entity,
        EntityManager entityManager,
        float deltaTime,
        UnitSourceResolver sources,
        bool captureDebug)
    {
        Entity = entity;
        EntityManager = entityManager;
        DeltaTime = deltaTime;
        Sources = sources;
        Debug.BeginFrame(captureDebug);
    }

    public void SetCurrentNode(ABehaviorNode node)
    {
        if (node != null)
            Debug.CurrentNodeName = node.DisplayName;
    }

    public void SetNodeStatus(string nodeGuid, BehaviorNodeStatus status)
    {
        Debug.SetNodeStatus(nodeGuid, status);
    }
}
public sealed class BehaviorDebugState
{
    private readonly Dictionary<string, BehaviorNodeStatus> _nodeStatuses = new(StringComparer.Ordinal);
    private bool _isEnabled;

    public string CurrentNodeName;
    public string LastStatus;

    public void BeginFrame(bool isEnabled)
    {
        _isEnabled = isEnabled;
        CurrentNodeName = "None";
        LastStatus = "None";
        _nodeStatuses.Clear();
    }

    public void SetNodeStatus(string nodeGuid, BehaviorNodeStatus status)
    {
        if (_isEnabled && !string.IsNullOrWhiteSpace(nodeGuid))
            _nodeStatuses[nodeGuid] = status;
    }

    public bool TryGetNodeStatus(string nodeGuid, out BehaviorNodeStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(nodeGuid) && _nodeStatuses.TryGetValue(nodeGuid, out status);
    }
}

public sealed class BehaviorTreeRuntime
{
    private readonly ABehaviorNode _root;
    private BehaviorContext _lastContext;

    public BehaviorTreeRuntime(ABehaviorNode root)
    {
        _root = root;
    }

    public bool IsValid => _root != null;
    public bool IsBound { get; private set; }
    public string BindingError { get; private set; } = string.Empty;
    public UnitSourceResolver Sources { get; private set; }

    public bool TryBind(UnitSourceResolver sources, out string error)
    {
        Sources = sources;
        if (_root == null)
        {
            IsBound = false;
            error = "Behavior tree root is missing.";
            BindingError = error;
            return false;
        }

        IsBound = _root.TryBind(sources, out error);
        BindingError = error ?? string.Empty;
        return IsBound;
    }

    public BehaviorNodeStatus Tick(BehaviorContext context)
    {
        _lastContext = context;
        if (_root == null || !IsBound || context?.Sources == null)
            return BehaviorNodeStatus.Failure;

        BehaviorNodeStatus status = _root.Tick(context);
        context.Debug.LastStatus = status.ToString();
        return status;
    }

    public bool TryGetDebugNodeStatus(string nodeGuid, out BehaviorNodeStatus status)
    {
        status = default;
        return _lastContext != null && _lastContext.Debug.TryGetNodeStatus(nodeGuid, out status);
    }

    public void Reset()
    {
        _root?.Reset();
    }
}

public static class BehaviorTreeBuilder
{
    private static BehaviorNodeFactory s_factory;

    public static BehaviorTreeRuntime Build(
        BehaviorTreeData data,
        UnitSourceResolver sources,
        out string error)
    {
        error = string.Empty;
        if (data == null || data.Nodes == null || data.Nodes.Count == 0)
        {
            error = "Behavior tree has no nodes.";
            return null;
        }

        BehaviorNodeFactory factory = GetFactory();
        Dictionary<string, ABehaviorNode> runtimeNodes = new(StringComparer.Ordinal);
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
            if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid) ||
                !runtimeNodes.TryGetValue(nodeData.Guid, out ABehaviorNode node))
            {
                continue;
            }

            nodeData.ChildGuids ??= new List<string>();
            for (int childIndex = 0; childIndex < nodeData.ChildGuids.Count; childIndex++)
            {
                string childGuid = nodeData.ChildGuids[childIndex];
                if (!string.IsNullOrWhiteSpace(childGuid) && runtimeNodes.TryGetValue(childGuid, out ABehaviorNode child))
                    node.AddChild(child);
            }
        }

        if (string.IsNullOrWhiteSpace(data.RootNodeGuid) ||
            !runtimeNodes.TryGetValue(data.RootNodeGuid, out ABehaviorNode root))
        {
            error = "Behavior tree root is missing.";
            return null;
        }

        BehaviorTreeRuntime runtime = new(root);
        if (!runtime.TryBind(sources, out error))
            return null;

        return runtime;
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
