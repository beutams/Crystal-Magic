using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.Unit
{
    public sealed class StateScriptGraphView : GraphView
    {
        private readonly StateScriptEditorWindow _window;
        private readonly Dictionary<string, StateScriptNodeView> _nodeViews = new(StringComparer.Ordinal);

        public StateScriptGraphView(StateScriptEditorWindow window)
        {
            _window = window;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseUpEvent>(_ => _window.NotifyGraphNodeSelected(GetSelectedNodeData()?.Guid));
            RegisterCallback<KeyUpEvent>(_ => _window.NotifyGraphNodeSelected(GetSelectedNodeData()?.Guid));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            List<Port> results = new();
            foreach (Port port in ports)
            {
                if (port == startPort || port.node == startPort.node || port.direction == startPort.direction)
                    continue;

                results.Add(port);
            }

            return results;
        }

        public void BuildFromData(StateScriptInstanceData graph)
        {
            ClearGraph();
            if (graph?.Nodes == null)
                return;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                StateScriptNodeData nodeData = graph.Nodes[i];
                StateScriptNode prototype = StateScriptRuntimeBuilder.CreatePrototype(nodeData);
                if (nodeData == null || prototype == null)
                    continue;

                Rect position = new(nodeData.EditorPosition, Vector2.zero);
                if (position.position == Vector2.zero && i > 0)
                    position.position = new Vector2(260f + i * 40f, 150f + i * 30f);

                AddNodeView(nodeData, prototype, position);
            }

            var savedCallback = graphViewChanged;
            graphViewChanged = null;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                StateScriptEdgeData edgeData = graph.Edges[i];
                if (edgeData == null ||
                    !_nodeViews.TryGetValue(edgeData.OutputNodeGuid, out StateScriptNodeView outputView) ||
                    !_nodeViews.TryGetValue(edgeData.InputNodeGuid, out StateScriptNodeView inputView) ||
                    !outputView.TryGetOutputPort(edgeData.OutputPortName, out Port output) ||
                    !inputView.TryGetInputPort(edgeData.InputPortName, out Port input))
                {
                    continue;
                }

                AddElement(output.ConnectTo(input));
            }

            graphViewChanged = savedCallback;
            UpdateViewTransform(graph.ViewPosition, Vector3.one * graph.ViewScale);
        }

        public StateScriptNodeData GetSelectedNodeData()
        {
            return selection?.OfType<StateScriptNodeView>().FirstOrDefault()?.NodeData;
        }

        public bool SelectNode(string nodeGuid)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid) || !_nodeViews.TryGetValue(nodeGuid, out StateScriptNodeView view))
                return false;

            ClearSelection();
            AddToSelection(view);
            return true;
        }

        private bool AddNode(StateScriptNodeData nodeData)
        {
            if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid) || _nodeViews.ContainsKey(nodeData.Guid))
                return false;

            StateScriptNode prototype = StateScriptRuntimeBuilder.CreatePrototype(nodeData);
            if (prototype == null)
                return false;

            StateScriptNodeView view = AddNodeView(nodeData, prototype, new Rect(nodeData.EditorPosition, Vector2.zero));
            ClearSelection();
            AddToSelection(view);
            return true;
        }

        public bool SaveViewTransform(StateScriptInstanceData graph)
        {
            if (graph == null)
                return false;

            Vector2 position = viewTransform.position;
            float scale = Mathf.Max(0.1f, viewTransform.scale.x);
            bool changed = (graph.ViewPosition - position).sqrMagnitude > 0.001f ||
                !Mathf.Approximately(graph.ViewScale, scale);
            graph.ViewPosition = position;
            graph.ViewScale = scale;
            return changed;
        }

        // The graph view is the editing surface. Commit its current nodes, edges, and view
        // before persistence so the saved data always represents what is currently visible.
        public void SynchronizeToData(StateScriptInstanceData graph)
        {
            if (graph == null)
                return;

            graph.EnsureValid();
            SaveViewTransform(graph);

            List<StateScriptNodeData> visibleNodes = _nodeViews.Values
                .Select(view => view.NodeData)
                .Where(node => node != null)
                .ToList();
            HashSet<StateScriptNodeData> visibleNodeSet = new(visibleNodes);

            // Keep the existing node order stable while dropping only nodes deleted from the view.
            graph.Nodes = graph.Nodes
                .Where(node => node != null && visibleNodeSet.Contains(node))
                .ToList();
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                StateScriptNodeData node = visibleNodes[i];
                if (!graph.Nodes.Contains(node))
                    graph.Nodes.Add(node);
            }

            List<StateScriptEdgeData> visibleEdges = new();
            foreach (Edge edge in edges)
            {
                if (!TryCreateEdgeData(edge, out StateScriptEdgeData edgeData) || ContainsEdge(visibleEdges, edgeData))
                    continue;

                visibleEdges.Add(edgeData);
            }

            graph.Edges = visibleEdges;
        }

        public void RefreshRuntimeDebug(StateScriptRuntime runtime)
        {
            foreach (StateScriptNodeView view in _nodeViews.Values)
            {
                StateScriptNode node = null;
                runtime?.TryGetNode(view.NodeData.Guid, out node);
                view.RefreshRuntimeDebug(node, runtime);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            StateScriptInstanceData graph = _window.SelectedGraph;
            if (graph != null)
            {
                Vector2 position = contentViewContainer.WorldToLocal(this.LocalToWorld(evt.localMousePosition));
                bool hasEntry = graph.Nodes?.OfType<StateScriptEntryNodeData>().Any() == true;
                IReadOnlyList<FactoryTypeInfo> types = StateScriptNodeDataRegistry.TypeInfos;
                for (int i = 0; i < types.Count; i++)
                {
                    FactoryTypeInfo type = types[i];
                    if (hasEntry && string.Equals(type.Key, "Entry", StringComparison.Ordinal))
                        continue;

                    FactoryTypeInfo capturedType = type;
                    evt.menu.AppendAction($"Add Node/{capturedType.DisplayName}", _ =>
                    {
                        StateScriptInstanceData selectedGraph = _window.SelectedGraph;
                        if (selectedGraph == null)
                            return;

                        StateScriptNodeData node = StateScriptNodeDataRegistry.Create(capturedType.Key);
                        if (node == null)
                            return;

                        node.EditorPosition = position;

                        if (!AddNode(node))
                            return;

                        selectedGraph.Nodes.Add(node);
                        if (node is StateScriptEntryNodeData)
                            selectedGraph.EntryNodeGuid = node.Guid;

                        _window.MarkDirty();
                    });
                }
            }

            base.BuildContextualMenu(evt);
        }

        private void ClearGraph()
        {
            var savedCallback = graphViewChanged;
            graphViewChanged = null;

            foreach (GraphElement element in graphElements.ToList())
            {
                if (element is Edge || element is Node)
                    RemoveElement(element);
            }

            _nodeViews.Clear();
            graphViewChanged = savedCallback;
        }

        private StateScriptNodeView AddNodeView(StateScriptNodeData nodeData, StateScriptNode prototype, Rect position)
        {
            StateScriptNodeView view = new(nodeData, prototype);
            view.SetPosition(position);
            AddElement(view);
            _nodeViews.Add(nodeData.Guid, view);
            return view;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            StateScriptInstanceData graph = _window.SelectedGraph;
            if (graph == null)
                return change;

            graph.EnsureValid();
            bool changed = false;

            if (change.edgesToCreate != null)
            {
                foreach (Edge edge in change.edgesToCreate)
                {
                    if (!TryCreateEdgeData(edge, out StateScriptEdgeData edgeData) || ContainsEdge(graph, edgeData))
                        continue;

                    graph.Edges.Add(edgeData);
                    changed = true;
                }
            }

            if (change.elementsToRemove != null)
            {
                foreach (GraphElement element in change.elementsToRemove)
                {
                    if (element is Edge edge && TryCreateEdgeData(edge, out StateScriptEdgeData edgeData))
                    {
                        changed |= graph.Edges.RemoveAll(existing => IsSameEdge(existing, edgeData)) > 0;
                    }
                    else if (element is StateScriptNodeView nodeView)
                    {
                        graph.Nodes.Remove(nodeView.NodeData);
                        graph.Edges.RemoveAll(edge => IsConnectedTo(edge, nodeView.NodeData.Guid));
                        _nodeViews.Remove(nodeView.NodeData.Guid);
                        if (string.Equals(graph.EntryNodeGuid, nodeView.NodeData.Guid, StringComparison.Ordinal))
                            graph.EntryNodeGuid = string.Empty;
                        changed = true;
                    }
                }
            }

            if (change.movedElements is { Count: > 0 })
            {
                foreach (GraphElement element in change.movedElements)
                {
                    if (element is StateScriptNodeView nodeView)
                        nodeView.NodeData.EditorPosition = nodeView.GetPosition().position;
                }

                changed = true;
            }

            if (changed)
                _window.NotifyGraphChanged();

            return change;
        }

        private static bool TryCreateEdgeData(Edge edge, out StateScriptEdgeData edgeData)
        {
            edgeData = null;
            if (edge?.output?.node is not StateScriptNodeView outputView ||
                edge.input?.node is not StateScriptNodeView inputView ||
                string.IsNullOrWhiteSpace(edge.output.portName) ||
                string.IsNullOrWhiteSpace(edge.input.portName))
            {
                return false;
            }

            edgeData = new StateScriptEdgeData
            {
                OutputNodeGuid = outputView.NodeData.Guid,
                OutputPortName = edge.output.portName,
                InputNodeGuid = inputView.NodeData.Guid,
                InputPortName = edge.input.portName,
            };
            return true;
        }

        private static bool ContainsEdge(StateScriptInstanceData graph, StateScriptEdgeData candidate)
        {
            return graph.Edges.Any(edge => IsSameEdge(edge, candidate));
        }

        private static bool ContainsEdge(List<StateScriptEdgeData> edges, StateScriptEdgeData candidate)
        {
            return edges.Any(edge => IsSameEdge(edge, candidate));
        }

        private static bool IsSameEdge(StateScriptEdgeData left, StateScriptEdgeData right)
        {
            return left != null && right != null &&
                string.Equals(left.OutputNodeGuid, right.OutputNodeGuid, StringComparison.Ordinal) &&
                string.Equals(left.OutputPortName, right.OutputPortName, StringComparison.Ordinal) &&
                string.Equals(left.InputNodeGuid, right.InputNodeGuid, StringComparison.Ordinal) &&
                string.Equals(left.InputPortName, right.InputPortName, StringComparison.Ordinal);
        }

        private static bool IsConnectedTo(StateScriptEdgeData edge, string nodeGuid)
        {
            return edge != null &&
                (string.Equals(edge.OutputNodeGuid, nodeGuid, StringComparison.Ordinal) ||
                string.Equals(edge.InputNodeGuid, nodeGuid, StringComparison.Ordinal));
        }
    }

    public sealed class StateScriptNodeView : Node
    {
        private static readonly Color s_pulseColor = new(0.72f, 0.56f, 0.18f, 1f);
        private static readonly Color s_pendingColor = new(0.70f, 0.40f, 0.12f, 1f);
        private static readonly Color s_runningColor = new(0.22f, 0.58f, 0.30f, 1f);
        private const double PulseFadeDurationSeconds = 0.5d;

        private readonly Dictionary<string, Port> _inputs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Port> _outputs = new(StringComparer.Ordinal);
        private bool _hasObservedPulse;
        private long _lastObservedPulseTick;
        private double _pulseStartedAt;

        public StateScriptNodeView(StateScriptNodeData nodeData, StateScriptNode prototype)
        {
            NodeData = nodeData;
            title = StateScriptNodeDataRegistry.GetDisplayName(nodeData.Type);
            viewDataKey = nodeData.Guid;

            for (int i = 0; i < prototype.Inputs.Count; i++)
                AddInputPort(prototype.Inputs[i].Name);
            for (int i = 0; i < prototype.Outputs.Count; i++)
                AddOutputPort(prototype.Outputs[i].Name);

            RefreshPorts();
            RefreshExpandedState();
        }

        public StateScriptNodeData NodeData { get; }

        public bool TryGetInputPort(string name, out Port port)
        {
            return _inputs.TryGetValue(name ?? string.Empty, out port);
        }

        public bool TryGetOutputPort(string name, out Port port)
        {
            return _outputs.TryGetValue(name ?? string.Empty, out port);
        }

        public void RefreshRuntimeDebug(StateScriptNode node, StateScriptRuntime runtime)
        {
            ObservePulse(node);
            if (node is StateScriptStateNode state)
            {
                if (state.Status == StateScriptStateStatus.Running)
                {
                    titleContainer.style.backgroundColor = s_runningColor;
                    return;
                }
                else if (state.Status == StateScriptStateStatus.Pending)
                {
                    titleContainer.style.backgroundColor = s_pendingColor;
                    return;
                }
            }
            else if (TryGetPulseColor(out Color pulseColor))
            {
                titleContainer.style.backgroundColor = pulseColor;
                return;
            }

            titleContainer.style.backgroundColor = new StyleColor(StyleKeyword.Null);
        }

        private void ObservePulse(StateScriptNode node)
        {
            if (node == null)
            {
                _hasObservedPulse = false;
                _lastObservedPulseTick = -1;
                _pulseStartedAt = 0d;
                return;
            }

            if (!_hasObservedPulse)
            {
                _hasObservedPulse = true;
                _lastObservedPulseTick = node.LastPulseTick;
                return;
            }

            if (node.LastPulseTick == _lastObservedPulseTick)
                return;

            _lastObservedPulseTick = node.LastPulseTick;
            if (node.LastPulseTick >= 0)
                _pulseStartedAt = EditorApplication.timeSinceStartup;
        }

        private bool TryGetPulseColor(out Color color)
        {
            double elapsed = EditorApplication.timeSinceStartup - _pulseStartedAt;
            if (_pulseStartedAt <= 0d || elapsed < 0d || elapsed >= PulseFadeDurationSeconds)
            {
                color = default;
                return false;
            }

            float fade = Mathf.SmoothStep(1f, 0f, (float)(elapsed / PulseFadeDurationSeconds));
            color = s_pulseColor;
            color.a = fade;
            return true;
        }

        private void AddInputPort(string name)
        {
            Port port = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            port.portName = name;
            inputContainer.Add(port);
            _inputs.Add(name, port);
        }

        private void AddOutputPort(string name)
        {
            Port port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            port.portName = name;
            outputContainer.Add(port);
            _outputs.Add(name, port);
        }
    }
}
