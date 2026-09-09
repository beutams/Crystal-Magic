using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Editor;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.Data
{
    public sealed class NPCInteractionGraphWindow : EditorWindow
    {
        private const float InspectorWidth = 350f;

        private NPCEditorWindow _owner;
        private NPCData _npc;
        private NPCInteractionData _interaction;
        private string _layoutKey;
        private NPCInteractionGraphLayoutStore _layoutStore;
        private NPCInteractionGraphLayoutData _layout;
        private NPCInteractionGraphView _graphView;
        private IMGUIContainer _inspector;
        private NPCInteractionNodeData _selectedNode;
        private bool _rebuildScheduled;

        public static void Open(NPCEditorWindow owner, NPCData npc, NPCInteractionData interaction)
        {
            if (npc == null || interaction == null)
                return;

            NPCInteractionGraphWindow window = CreateInstance<NPCInteractionGraphWindow>();
            window.titleContent = new GUIContent("NPC Interaction Graph");
            window.minSize = new Vector2(960f, 620f);
            window.Initialize(owner, npc, interaction);
            window.Show();
        }

        private void Initialize(NPCEditorWindow owner, NPCData npc, NPCInteractionData interaction)
        {
            _owner = owner;
            _npc = npc;
            _interaction = interaction;
            _layoutKey = GetLayoutKey(npc, interaction);
            _layoutStore = new NPCInteractionGraphLayoutStore();
            _layout = _layoutStore.Load(_layoutKey);
            BuildRoot();
        }

        private void OnEnable()
        {
            if (_interaction != null && _graphView == null)
                BuildRoot();
        }

        private void OnDisable()
        {
            _graphView?.SaveLayout();
        }

        internal NPCInteractionData Interaction => _interaction;

        internal void RebuildGraph()
        {
            if (_interaction == null)
                return;

            _layout ??= _layoutStore?.Load(_layoutKey) ?? new NPCInteractionGraphLayoutData();
            _graphView?.BuildFromData(_interaction, _layout);
            _inspector?.MarkDirtyRepaint();
        }

        internal void ScheduleGraphRebuild()
        {
            if (_rebuildScheduled)
                return;

            _rebuildScheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _rebuildScheduled = false;
                RebuildGraph();
            }).ExecuteLater(0);
        }

        internal void SetSelection(NPCInteractionNodeData node)
        {
            _selectedNode = node;
            _inspector?.MarkDirtyRepaint();
        }

        internal void MarkDataChanged()
        {
            _owner?.MarkDirtyFromGraph();
            _inspector?.MarkDirtyRepaint();
        }

        internal void SaveLayout(NPCInteractionGraphLayoutData layout, ISet<string> validNodeGuids)
        {
            if (_layoutStore == null || layout == null)
                return;

            _layout = layout;
            _layoutStore.Save(_layoutKey, layout, validNodeGuids);
        }

        private void BuildRoot()
        {
            rootVisualElement.Clear();
            if (_interaction == null)
                return;

            VisualElement toolbar = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = 24f,
                    paddingLeft = 6f,
                },
            };
            toolbar.Add(new Label($"{_npc?.NPC ?? "NPC"} / {_interaction.Key ?? "Interaction"}") { style = { flexGrow = 1f } });
            toolbar.Add(new Button(() => _graphView?.SaveLayout()) { text = "Save Layout" });
            toolbar.Add(new Button(() => _owner?.SaveDataFromGraph()) { text = "Save NPC Data" });
            rootVisualElement.Add(toolbar);

            VisualElement content = new() { style = { flexGrow = 1f, flexDirection = FlexDirection.Row } };
            _graphView = new NPCInteractionGraphView(this) { style = { flexGrow = 1f } };
            content.Add(_graphView);

            _inspector = new IMGUIContainer(DrawInspector)
            {
                style =
                {
                    width = InspectorWidth,
                    minWidth = InspectorWidth,
                    borderLeftWidth = 1f,
                    borderLeftColor = new Color(0.2f, 0.2f, 0.2f),
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 8f,
                },
            };
            content.Add(_inspector);
            rootVisualElement.Add(content);
            RebuildGraph();
        }

        private void DrawInspector()
        {
            if (_selectedNode == null || _interaction?.GetNode(_selectedNode.Guid) != _selectedNode)
            {
                EditorGUILayout.HelpBox("Select an interaction node to edit it. Create nodes with the graph's right-click menu.", MessageType.Info);
                return;
            }

            string typeName = ResolveNodeTypeName(_selectedNode);
            EditorGUILayout.LabelField(NPCInteractionNodeDataRegistry.GetDisplayName(typeName), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Guid", _selectedNode.Guid ?? string.Empty, EditorStyles.miniLabel);
            DrawNodeTypeMenu(typeName);
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();
            switch (_selectedNode)
            {
                case NPCDialogueInteractionNodeData dialogue:
                    dialogue.Speaker = EditorGUILayout.TextField("Speaker", dialogue.Speaker ?? string.Empty);
                    dialogue.ContentKey = EditorGUILayout.TextField("Content Key", dialogue.ContentKey ?? string.Empty);
                    DrawBranches(dialogue);
                    break;

                case NPCSelectInteractionNodeData select:
                    DrawSelect(select);
                    break;

                case NPCOpenUIInteractionNodeData openUI:
                    openUI.UIName = EditorGUILayout.TextField("UI Name", openUI.UIName ?? string.Empty);
                    openUI.OpenData = EditorGUILayout.TextField("Open Data", openUI.OpenData ?? string.Empty);
                    openUI.WaitUntilClosed = EditorGUILayout.Toggle("Wait Until Closed", openUI.WaitUntilClosed);
                    DrawBranches(openUI);
                    break;

                case NPCMoveInteractionNodeData move:
                    move.TargetMarker = EditorGUILayout.TextField("Target Marker", move.TargetMarker ?? string.Empty);
                    move.StopDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Stop Distance", move.StopDistance));
                    move.WaitUntilArrived = EditorGUILayout.Toggle("Wait Until Arrived", move.WaitUntilArrived);
                    DrawBranches(move);
                    break;

                case NPCEnterDungeonInteractionNodeData enterDungeon:
                    enterDungeon.DungeonFloor = Mathf.Max(1, EditorGUILayout.IntField("Dungeon Floor", enterDungeon.DungeonFloor));
                    EditorGUILayout.HelpBox("This node ends the interaction and enters the dungeon flow.", MessageType.None);
                    break;

                case NPCEnterTrainingGroundInteractionNodeData:
                    EditorGUILayout.HelpBox("This node ends the interaction and enters the training ground flow.", MessageType.None);
                    break;

                case NPCEnterTownInteractionNodeData:
                    EditorGUILayout.HelpBox("This node ends the interaction and enters the town flow.", MessageType.None);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
                MarkDataChanged();
        }

        private void DrawNodeTypeMenu(string currentTypeName)
        {
            if (!GUILayout.Button("Change Node Type", GUILayout.Width(130f)))
                return;

            GenericMenu menu = new();
            foreach (string typeName in NPCInteractionNodeDataRegistry.TypeOrder)
            {
                string capturedTypeName = typeName;
                menu.AddItem(
                    new GUIContent(NPCInteractionNodeDataRegistry.GetDisplayName(capturedTypeName)),
                    string.Equals(currentTypeName, capturedTypeName, StringComparison.Ordinal),
                    () => _graphView?.ChangeNodeType(_selectedNode, capturedTypeName));
            }
            menu.ShowAsContext();
        }

        private void DrawBranches(NPCInteractionNodeData node)
        {
            node.Branches ??= new List<NPCInteractionBranchData>();
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Branch", GUILayout.Width(92f)))
            {
                node.Branches.Add(new NPCInteractionBranchData());
                MarkDataChanged();
                ScheduleGraphRebuild();
            }
            EditorGUILayout.EndHorizontal();

            for (int index = 0; index < node.Branches.Count; index++)
            {
                NPCInteractionBranchData branch = node.Branches[index] ?? new NPCInteractionBranchData();
                node.Branches[index] = branch;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Branch {index + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    node.Branches.RemoveAt(index);
                    MarkDataChanged();
                    ScheduleGraphRebuild();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();
                branch.CheckExpression = EditorGUILayout.TextField("Check", branch.CheckExpression ?? string.Empty);
                EditorGUILayout.LabelField("Next Node", "Connect the bottom port in the graph.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            if (node.Branches.Count == 0)
                EditorGUILayout.HelpBox("Add a branch to create an output port.", MessageType.None);
        }

        private void DrawSelect(NPCSelectInteractionNodeData select)
        {
            select.Options ??= new List<NPCSelectOptionData>();
            select.Dialog = EditorGUILayout.TextField("Dialog", select.Dialog ?? string.Empty);
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Option", GUILayout.Width(92f)))
            {
                select.Options.Add(new NPCSelectOptionData
                {
                    DisplayNameKey = $"npc.option_{select.Options.Count + 1}.name",
                });
                MarkDataChanged();
                ScheduleGraphRebuild();
            }
            EditorGUILayout.EndHorizontal();

            for (int index = 0; index < select.Options.Count; index++)
            {
                NPCSelectOptionData option = select.Options[index] ?? new NPCSelectOptionData();
                select.Options[index] = option;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Option {index + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    select.Options.RemoveAt(index);
                    MarkDataChanged();
                    ScheduleGraphRebuild();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();
                option.DisplayNameKey = EditorGUILayout.TextField("Display Name Key", option.DisplayNameKey ?? string.Empty);
                option.EnableExpression = EditorGUILayout.TextField("Enable Expression", option.EnableExpression ?? string.Empty);
                EditorGUILayout.LabelField("Next Node", "Connect the bottom port in the graph.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            if (select.Options.Count == 0)
                EditorGUILayout.HelpBox("Add an option to create an output port.", MessageType.None);
        }

        private static string GetLayoutKey(NPCData npc, NPCInteractionData interaction)
        {
            int interactionIndex = npc?.Interactions?.IndexOf(interaction) ?? -1;
            return $"NPC:{npc?.PrefabPath ?? npc?.NPC ?? "Unknown"}:Interaction:{interactionIndex}:{interaction?.Key ?? "Unnamed"}";
        }

        internal static string ResolveNodeTypeName(NPCInteractionNodeData node)
        {
            return node != null && NPCInteractionNodeDataRegistry.TryGetNodeKey(node.GetType(), out string typeName)
                ? typeName
                : string.Empty;
        }

        internal static Color GetNodeColor(NPCInteractionNodeData node)
        {
            return node switch
            {
                NPCDialogueInteractionNodeData => new Color(0.81f, 0.56f, 0.20f),
                NPCSelectInteractionNodeData => new Color(0.69f, 0.40f, 0.78f),
                NPCOpenUIInteractionNodeData => new Color(0.43f, 0.51f, 0.84f),
                NPCMoveInteractionNodeData => new Color(0.35f, 0.68f, 0.66f),
                NPCEnterDungeonInteractionNodeData => new Color(0.79f, 0.30f, 0.33f),
                NPCEnterTrainingGroundInteractionNodeData => new Color(0.30f, 0.56f, 0.81f),
                NPCEnterTownInteractionNodeData => new Color(0.32f, 0.56f, 0.81f),
                _ => new Color(0.52f, 0.52f, 0.52f),
            };
        }
    }

    internal sealed class NPCInteractionGraphView : GraphView
    {
        private const float NodeWidth = 230f;
        private const float NodeHeight = 112f;

        private readonly NPCInteractionGraphWindow _window;
        private readonly Dictionary<string, NPCInteractionNodeView> _nodeViews = new(StringComparer.Ordinal);
        private NPCInteractionGraphEntryView _entryView;
        private NPCInteractionGraphLayoutData _layout;
        private bool _isBuilding;
        private bool _linkSyncScheduled;

        public NPCInteractionGraphView(NPCInteractionGraphWindow window)
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
        }

        public void BuildFromData(NPCInteractionData interaction, NPCInteractionGraphLayoutData layout)
        {
            if (interaction == null)
                return;

            _isBuilding = true;
            _layout = layout ?? new NPCInteractionGraphLayoutData();
            _layout.Nodes ??= new List<NPCInteractionGraphNodeLayout>();
            ClearGraph();

            _entryView = new NPCInteractionGraphEntryView();
            _entryView.SetPosition(new Rect(76f, 62f, 130f, 58f));
            AddElement(_entryView);

            interaction.Nodes ??= new List<NPCInteractionNodeData>();
            for (int index = 0; index < interaction.Nodes.Count; index++)
            {
                NPCInteractionNodeData node = interaction.Nodes[index];
                if (node == null)
                    continue;

                if (string.IsNullOrWhiteSpace(node.Guid))
                {
                    node.Guid = Guid.NewGuid().ToString("N");
                    _window.MarkDataChanged();
                }

                NPCInteractionNodeView view = new(node, this);
                view.SetPosition(new Rect(GetNodePosition(node.Guid, index), new Vector2(NodeWidth, NodeHeight)));
                AddElement(view);
                _nodeViews[node.Guid] = view;
            }

            if (_nodeViews.TryGetValue(interaction.EntryNodeGuid ?? string.Empty, out NPCInteractionNodeView entryNode))
                AddElement(_entryView.Output.ConnectTo(entryNode.Input));

            foreach (NPCInteractionNodeView source in _nodeViews.Values)
            {
                foreach (NPCInteractionGraphOutputLink link in source.OutputLinks)
                {
                    if (_nodeViews.TryGetValue(link.GetNextNodeGuid() ?? string.Empty, out NPCInteractionNodeView target))
                        AddElement(link.Port.ConnectTo(target.Input));
                }
            }

            UpdateViewTransform(_layout.ViewPosition, Vector3.one * Mathf.Max(0.1f, _layout.ViewScale));
            _isBuilding = false;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            if (startPort == null)
                return new List<Port>();

            if (startPort.direction == Direction.Output)
            {
                return _nodeViews.Values
                    .Where(view => !ReferenceEquals(view, startPort.node))
                    .Select(view => view.Input)
                    .ToList();
            }

            List<Port> outputs = new();
            if (!ReferenceEquals(_entryView, startPort.node))
                outputs.Add(_entryView.Output);

            foreach (NPCInteractionNodeView view in _nodeViews.Values)
            {
                if (ReferenceEquals(view, startPort.node))
                    continue;

                outputs.AddRange(view.OutputLinks.Select(link => link.Port));
            }

            return outputs;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 position = contentViewContainer.WorldToLocal(this.LocalToWorld(evt.localMousePosition));
            foreach (string typeName in NPCInteractionNodeDataRegistry.TypeOrder)
            {
                string capturedTypeName = typeName;
                evt.menu.AppendAction(
                    $"Create Node/{NPCInteractionNodeDataRegistry.GetDisplayName(capturedTypeName)}",
                    _ => CreateNode(capturedTypeName, position));
            }

            base.BuildContextualMenu(evt);
        }

        public void SelectNode(NPCInteractionNodeData node)
        {
            _window.SetSelection(node);
        }

        public void ChangeNodeType(NPCInteractionNodeData node, string typeName)
        {
            NPCInteractionData interaction = _window.Interaction;
            int index = interaction?.GetNodeIndex(node?.Guid) ?? -1;
            if (index < 0 || string.Equals(NPCInteractionGraphWindow.ResolveNodeTypeName(node), typeName, StringComparison.Ordinal))
                return;

            NPCInteractionNodeData replacement = NPCInteractionNodeDataFactory.Default.CreateNode(typeName);
            if (replacement == null)
                return;

            replacement.Guid = node.Guid;
            replacement.Branches = node.Branches ?? new List<NPCInteractionBranchData>();
            interaction.Nodes[index] = replacement;
            _window.SetSelection(replacement);
            _window.MarkDataChanged();
            _window.ScheduleGraphRebuild();
        }

        public void SaveLayout()
        {
            if (_layout == null)
                return;

            _layout.ViewPosition = viewTransform.position;
            _layout.ViewScale = viewTransform.scale.x;
            _layout.Nodes.Clear();
            HashSet<string> validNodeGuids = new(StringComparer.Ordinal);
            foreach ((string guid, NPCInteractionNodeView view) in _nodeViews)
            {
                validNodeGuids.Add(guid);
                _layout.Nodes.Add(new NPCInteractionGraphNodeLayout
                {
                    Guid = guid,
                    Position = view.GetPosition().position,
                });
            }

            _window.SaveLayout(_layout, validNodeGuids);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_isBuilding)
                return change;

            bool linksChanged = change.edgesToCreate is { Count: > 0 };
            bool positionsChanged = false;
            if (change.elementsToRemove != null)
            {
                foreach (GraphElement element in change.elementsToRemove)
                {
                    if (element is Edge)
                    {
                        linksChanged = true;
                        continue;
                    }

                    if (element is not NPCInteractionNodeView nodeView)
                        continue;

                    _window.Interaction.Nodes.Remove(nodeView.NodeData);
                    _nodeViews.Remove(nodeView.NodeData.Guid);
                    _layout?.Nodes.RemoveAll(node => string.Equals(node?.Guid, nodeView.NodeData.Guid, StringComparison.Ordinal));
                    _window.SetSelection(null);
                    linksChanged = true;
                }
            }

            if (change.movedElements is { Count: > 0 })
            {
                positionsChanged = change.movedElements.OfType<NPCInteractionNodeView>().Any();
            }

            if (linksChanged)
                ScheduleLinkSync();
            if (positionsChanged)
                SaveLayout();

            return change;
        }

        private void CreateNode(string typeName, Vector2 position)
        {
            NPCInteractionNodeData node = NPCInteractionNodeDataFactory.Default.CreateNode(typeName);
            if (node == null)
                return;

            _window.Interaction.Nodes ??= new List<NPCInteractionNodeData>();
            _window.Interaction.Nodes.Add(node);
            _layout ??= new NPCInteractionGraphLayoutData();
            _layout.Nodes.Add(new NPCInteractionGraphNodeLayout { Guid = node.Guid, Position = position });
            _window.MarkDataChanged();
            _window.SetSelection(node);
            _window.ScheduleGraphRebuild();
        }

        private void ScheduleLinkSync()
        {
            if (_linkSyncScheduled)
                return;

            _linkSyncScheduled = true;
            schedule.Execute(() =>
            {
                _linkSyncScheduled = false;
                if (_isBuilding)
                    return;

                SyncLinksFromGraph();
                _window.MarkDataChanged();
            }).ExecuteLater(0);
        }

        private void SyncLinksFromGraph()
        {
            NPCInteractionData interaction = _window.Interaction;
            if (interaction == null)
                return;

            interaction.EntryNodeGuid = GetTargetGuid(_entryView?.Output);
            foreach (NPCInteractionNodeView view in _nodeViews.Values)
            {
                foreach (NPCInteractionGraphOutputLink link in view.OutputLinks)
                    link.SetNextNodeGuid(GetTargetGuid(link.Port));
            }
        }

        private static string GetTargetGuid(Port output)
        {
            if (output == null)
                return string.Empty;

            foreach (Edge edge in output.connections)
            {
                if (edge?.input?.node is NPCInteractionNodeView target)
                    return target.NodeData.Guid;
            }

            return string.Empty;
        }

        private void ClearGraph()
        {
            foreach (GraphElement element in graphElements.ToList())
                RemoveElement(element);

            _nodeViews.Clear();
            _entryView = null;
        }

        private Vector2 GetNodePosition(string guid, int fallbackIndex)
        {
            NPCInteractionGraphNodeLayout saved = _layout.Nodes.FirstOrDefault(node =>
                node != null && string.Equals(node.Guid, guid, StringComparison.Ordinal));
            return saved != null
                ? saved.Position
                : new Vector2(300f + fallbackIndex % 3 * 300f, 110f + fallbackIndex / 3 * 220f);
        }
    }

    internal sealed class NPCInteractionGraphEntryView : TopBottomPortNode
    {
        public NPCInteractionGraphEntryView()
        {
            title = "Entry";
            capabilities = Capabilities.Selectable;
            Output = CreateBottomOutput("Start", Port.Capacity.Single, typeof(bool));
        }

        public Port Output { get; }
    }

    internal sealed class NPCInteractionGraphOutputLink
    {
        private readonly Func<string> _getNextNodeGuid;
        private readonly Action<string> _setNextNodeGuid;

        public NPCInteractionGraphOutputLink(Port port, Func<string> getNextNodeGuid, Action<string> setNextNodeGuid)
        {
            Port = port;
            _getNextNodeGuid = getNextNodeGuid;
            _setNextNodeGuid = setNextNodeGuid;
        }

        public Port Port { get; }

        public string GetNextNodeGuid()
        {
            return _getNextNodeGuid();
        }

        public void SetNextNodeGuid(string guid)
        {
            _setNextNodeGuid(guid ?? string.Empty);
        }
    }

    internal sealed class NPCInteractionNodeView : TopBottomPortNode
    {
        private readonly List<NPCInteractionGraphOutputLink> _outputLinks = new();

        public NPCInteractionNodeView(NPCInteractionNodeData nodeData, NPCInteractionGraphView graphView)
        {
            NodeData = nodeData;
            title = NPCInteractionNodeDataRegistry.GetDisplayName(NPCInteractionGraphWindow.ResolveNodeTypeName(nodeData));
            titleContainer.style.backgroundColor = NPCInteractionGraphWindow.GetNodeColor(nodeData);
            viewDataKey = nodeData.Guid;
            Input = CreateTopInput("Input", Port.Capacity.Multi, typeof(bool));
            CreateOutputPorts();
            capabilities = Capabilities.Movable | Capabilities.Selectable | Capabilities.Deletable;
            RegisterCallback<MouseDownEvent>(_ => graphView.SelectNode(NodeData));
        }

        public NPCInteractionNodeData NodeData { get; }

        public Port Input { get; }

        public IReadOnlyList<NPCInteractionGraphOutputLink> OutputLinks => _outputLinks;

        private void CreateOutputPorts()
        {
            if (NodeData is NPCSelectInteractionNodeData select)
            {
                select.Options ??= new List<NPCSelectOptionData>();
                for (int index = 0; index < select.Options.Count; index++)
                {
                    NPCSelectOptionData option = select.Options[index] ?? new NPCSelectOptionData();
                    select.Options[index] = option;
                    Port port = CreateBottomOutput($"Option {index + 1}", Port.Capacity.Single, typeof(bool));
                    _outputLinks.Add(new NPCInteractionGraphOutputLink(
                        port,
                        () => option.NextNodeGuid,
                        guid => option.NextNodeGuid = guid));
                }
                return;
            }

            if (NodeData is NPCEnterDungeonInteractionNodeData or NPCEnterTrainingGroundInteractionNodeData or NPCEnterTownInteractionNodeData)
                return;

            NodeData.Branches ??= new List<NPCInteractionBranchData>();
            for (int index = 0; index < NodeData.Branches.Count; index++)
            {
                NPCInteractionBranchData branch = NodeData.Branches[index] ?? new NPCInteractionBranchData();
                NodeData.Branches[index] = branch;
                Port port = CreateBottomOutput($"Branch {index + 1}", Port.Capacity.Single, typeof(bool));
                _outputLinks.Add(new NPCInteractionGraphOutputLink(
                    port,
                    () => branch.NextNodeGuid,
                    guid => branch.NextNodeGuid = guid));
            }
        }
    }
}
