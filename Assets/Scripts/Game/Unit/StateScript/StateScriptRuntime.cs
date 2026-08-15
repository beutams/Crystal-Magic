using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;

public sealed class StateScriptRuntime
{
    private const int MaxPulseDepth = 128;

    private readonly Dictionary<string, StateScriptNode> _nodes = new(StringComparer.Ordinal);
    private readonly List<StateScriptNode> _nodesInTraversalOrder = new();
    private readonly List<StateScriptStateNode> _statesInTickOrder = new();
    private StateScriptEntryNode _entry;
    private int _pulseDepth;

    internal StateScriptRuntime(
        StateScriptInstanceData data,
        Entity entity,
        EntityManager entityManager,
        UnitSourceAccessTable sources)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Entity = entity;
        EntityManager = entityManager;
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public StateScriptInstanceData Data { get; }
    public Entity Entity { get; }
    public EntityManager EntityManager { get; }
    public UnitSourceAccessTable Sources { get; }
    public float DeltaTime { get; private set; }
    public long TickVersion { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsBound { get; private set; }
    public string BindingError { get; private set; } = string.Empty;
    public IReadOnlyList<StateScriptNode> NodesInTraversalOrder => _nodesInTraversalOrder;
    public IReadOnlyList<StateScriptStateNode> StatesInTickOrder => _statesInTickOrder;

    public bool TryGetNode(string guid, out StateScriptNode node)
    {
        return _nodes.TryGetValue(guid ?? string.Empty, out node);
    }

    public void Start()
    {
        if (!IsBound || IsStarted)
            return;

        IsStarted = true;
        _entry.Start();
    }

    public void Tick(float deltaTime)
    {
        if (!IsBound)
            return;

        DeltaTime = deltaTime;
        TickVersion++;

        for (int i = 0; i < _statesInTickOrder.Count; i++)
            _statesInTickOrder[i].TryEnterRunning(TickVersion);

        for (int i = 0; i < _statesInTickOrder.Count; i++)
            _statesInTickOrder[i].TryUpdate();
    }

    public void StopAllWithoutOutput()
    {
        for (int i = 0; i < _statesInTickOrder.Count; i++)
            _statesInTickOrder[i].StopWithoutOutput();
    }

    internal bool TryEnterPulse()
    {
        if (_pulseDepth >= MaxPulseDepth)
        {
            BindingError = $"StateScript pulse depth exceeded {MaxPulseDepth} in graph: {Data.Name}";
            return false;
        }

        _pulseDepth++;
        return true;
    }

    internal void ExitPulse()
    {
        if (_pulseDepth > 0)
            _pulseDepth--;
    }

    internal void AddNode(StateScriptNode node)
    {
        _nodes.Add(node.Data.Guid, node);
    }

    internal void SetEntry(StateScriptEntryNode entry)
    {
        _entry = entry;
    }

    internal void CompleteBuild()
    {
        if (_entry == null)
        {
            BindingError = "StateScript graph has no Entry node.";
            return;
        }

        BuildTraversalOrder();
        if (_nodesInTraversalOrder.Count != _nodes.Count)
        {
            BindingError = "StateScript graph contains nodes unreachable from Entry.";
            return;
        }

        for (int i = 0; i < _nodesInTraversalOrder.Count; i++)
        {
            if (_nodesInTraversalOrder[i] is StateScriptStateNode state)
                _statesInTickOrder.Add(state);
        }

        IsBound = true;
        BindingError = string.Empty;
    }

    internal void FailBuild(string error)
    {
        IsBound = false;
        BindingError = error ?? string.Empty;
    }

    private void BuildTraversalOrder()
    {
        Queue<StateScriptNode> pending = new();
        HashSet<StateScriptNode> visited = new();
        pending.Enqueue(_entry);
        visited.Add(_entry);

        while (pending.Count > 0)
        {
            StateScriptNode current = pending.Dequeue();
            _nodesInTraversalOrder.Add(current);

            for (int outputIndex = 0; outputIndex < current.Outputs.Count; outputIndex++)
            {
                StateScriptOutputPort output = current.Outputs[outputIndex];
                for (int targetIndex = 0; targetIndex < output.Targets.Count; targetIndex++)
                {
                    StateScriptNode next = output.Targets[targetIndex].Owner;
                    if (visited.Add(next))
                        pending.Enqueue(next);
                }
            }
        }
    }
}

public static class StateScriptRuntimeBuilder
{
    private static readonly StateScriptNodeRuntimeFactory s_factory = CreateFactory();

    public static StateScriptRuntime Build(
        StateScriptInstanceData data,
        Entity entity,
        EntityManager entityManager,
        UnitSourceAccessTable sources,
        out string error)
    {
        error = string.Empty;
        if (data == null)
        {
            error = "StateScript graph data is missing.";
            return null;
        }

        data.EnsureValid();
        if (data.Nodes.Count == 0)
        {
            error = "StateScript graph has no nodes.";
            return null;
        }

        StateScriptRuntime runtime = new(data, entity, entityManager, sources);
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            StateScriptNodeData nodeData = data.Nodes[i];
            if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid))
            {
                error = "StateScript graph contains a node without Guid.";
                return null;
            }

            if (runtime.TryGetNode(nodeData.Guid, out _))
            {
                error = $"StateScript graph contains duplicate node Guid: {nodeData.Guid}";
                return null;
            }

            StateScriptNode node = s_factory.CreateNode(nodeData, runtime);
            if (node == null)
            {
                error = $"StateScript runtime is not registered for: {nodeData.Type}";
                return null;
            }

            if (!node.TryBind(out error))
                return null;

            runtime.AddNode(node);
            if (string.Equals(nodeData.Guid, data.EntryNodeGuid, StringComparison.Ordinal) &&
                node is StateScriptEntryNode entry)
            {
                runtime.SetEntry(entry);
            }
        }

        for (int i = 0; i < data.Edges.Count; i++)
        {
            StateScriptEdgeData edge = data.Edges[i];
            if (edge == null ||
                !runtime.TryGetNode(edge.OutputNodeGuid, out StateScriptNode outputNode) ||
                !runtime.TryGetNode(edge.InputNodeGuid, out StateScriptNode inputNode) ||
                !outputNode.TryGetOutput(edge.OutputPortName, out StateScriptOutputPort output) ||
                !inputNode.TryGetInput(edge.InputPortName, out StateScriptInputPort input))
            {
                error = "StateScript graph contains an invalid edge.";
                return null;
            }

            output.Connect(input);
        }

        runtime.CompleteBuild();
        if (!runtime.IsBound)
        {
            error = runtime.BindingError;
            return null;
        }

        return runtime;
    }

    public static StateScriptNode CreatePrototype(StateScriptNodeData data)
    {
        return data == null ? null : s_factory.CreateNode(data, null);
    }

    private static StateScriptNodeRuntimeFactory CreateFactory()
    {
        StateScriptNodeRuntimeFactory factory = new();
        StateScriptRegistry.RegisterAll(factory);
        return factory;
    }
}
