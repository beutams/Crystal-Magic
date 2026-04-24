using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.Unit
{
    public class BehaviorTreeGraphWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/BehaviorTreeDataTable.json";
        private const float ListPanelWidth = 240f;
        private const float InsertFieldWidth = 30f;

        private readonly List<BehaviorTreeData> _rows = new();
        private readonly Dictionary<BehaviorTreeData, string> _insertTexts = new();
        private int _selectedIndex = -1;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private Vector2 _listScrollPos;

        private BehaviorTreeGraphView _graphView;
        private IMGUIContainer _detailContainer;
        private Label _statusLabel;

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private sealed class TableWrapper
        {
            public List<BehaviorTreeData> Rows = new();
        }

        [MenuItem("Tools/Behavior Tree/Visual Editor")]
        public static void Open()
        {
            BehaviorTreeGraphWindow window = GetWindow<BehaviorTreeGraphWindow>("Behavior Tree");
            window.minSize = new Vector2(1200f, 680f);
            window.Show();
        }

        private void CreateGUI()
        {
            LoadData();

            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            BuildToolbar(root);
            BuildBody(root);

            if (_selectedIndex >= 0)
                RebuildGraph();
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.Add(MakeToolbarButton("Load", 48f, LoadData));
            toolbar.Add(MakeToolbarButton(_isDirty ? "Save *" : "Save", 58f, SaveData));
            toolbar.Add(MakeToolbarButton("Add", 44f, AddTree));
            toolbar.Add(MakeToolbarButton("Duplicate", 72f, DuplicateSelected));
            toolbar.Add(MakeToolbarButton("Delete", 58f, DeleteSelected));
            toolbar.Add(MakeToolbarButton("Validate", 64f, ValidateSelected));
            toolbar.Add(MakeToolbarButton("Generate Registry", 110f, BehaviorTreeRegistryGenerator.Generate));
            toolbar.Add(new VisualElement { style = { flexGrow = 1f } });

            _statusLabel = new Label(_statusText)
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleRight,
                    marginRight = 8f,
                }
            };
            toolbar.Add(_statusLabel);
            root.Add(toolbar);
        }

        private void BuildBody(VisualElement root)
        {
            var body = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1f,
                }
            };

            var listPanel = new IMGUIContainer(DrawListPanel)
            {
                style =
                {
                    width = ListPanelWidth,
                    minWidth = ListPanelWidth,
                }
            };
            body.Add(listPanel);
            body.Add(CreateDivider());

            _graphView = new BehaviorTreeGraphView(this)
            {
                style = { flexGrow = 1f }
            };
            _graphView.RegisterCallback<MouseUpEvent>(_ => _detailContainer?.MarkDirtyRepaint());
            _graphView.RegisterCallback<KeyUpEvent>(_ => _detailContainer?.MarkDirtyRepaint());
            body.Add(_graphView);
            body.Add(CreateDivider());

            var detailPanel = new VisualElement
            {
                style =
                {
                    width = 320f,
                    minWidth = 280f,
                    backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f),
                }
            };
            detailPanel.Add(new Label("Inspector")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 8f,
                    paddingTop = 6f,
                    paddingBottom = 4f,
                }
            });
            detailPanel.Add(CreateDivider());

            _detailContainer = new IMGUIContainer(DrawDetailPanel)
            {
                style = { flexGrow = 1f }
            };
            detailPanel.Add(_detailContainer);

            body.Add(detailPanel);
            root.Add(body);
        }

        private static VisualElement CreateDivider()
        {
            return new VisualElement
            {
                style =
                {
                    width = 1f,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f),
                }
            };
        }

        private static ToolbarButton MakeToolbarButton(string text, float width, Action onClick)
        {
            return new ToolbarButton(onClick) { text = text, style = { width = width } };
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Trees ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            Event evt = Event.current;
            BehaviorTreeData moveRow = null;
            int moveToIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                BehaviorTreeData row = _rows[i];
                EditorGUILayout.BeginHorizontal();
                string insertText = _insertTexts.TryGetValue(row, out string currentInsertText) ? currentInsertText : string.Empty;
                string controlName = $"insert_{row.GetHashCode()}";
                GUI.SetNextControlName(controlName);
                string newInsertText = EditorGUILayout.TextField(insertText, GUILayout.Width(InsertFieldWidth));
                if (newInsertText != insertText)
                {
                    if (string.IsNullOrWhiteSpace(newInsertText))
                        _insertTexts.Remove(row);
                    else
                        _insertTexts[row] = newInsertText;
                }

                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                bool submitByEnter = isFocused && evt.type == EventType.KeyDown &&
                    (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                bool submitByBlur = !isFocused && !string.IsNullOrWhiteSpace(newInsertText) && newInsertText == insertText;
                if ((submitByEnter || submitByBlur) && int.TryParse(newInsertText, out int insertTo))
                {
                    moveRow = row;
                    moveToIndex = Mathf.Clamp(insertTo - 1, 0, _rows.Count - 1);
                    _insertTexts.Remove(row);
                    if (submitByEnter)
                    {
                        evt.Use();
                        GUI.FocusControl(null);
                    }
                }

                bool isSelected = i == _selectedIndex;
                string label = $"[{row.Id}] {GetTreeName(row)}";
                if (GUILayout.Toggle(isSelected, label, "Button"))
                {
                    if (_selectedIndex != i)
                    {
                        _selectedIndex = i;
                        RebuildGraph();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            if (moveRow != null)
                MoveRowToInsertIndex(_rows.IndexOf(moveRow), moveToIndex);

            EditorGUILayout.EndVertical();
        }

        private void DrawDetailPanel()
        {
            if (_graphView == null)
                return;

            BehaviorTreeNodeView selectedNode = _graphView.selection?.OfType<BehaviorTreeNodeView>().FirstOrDefault();
            if (selectedNode == null)
            {
                EditorGUILayout.HelpBox("Select a node to edit its fields.", MessageType.Info);
                return;
            }

            BehaviorNodeData node = selectedNode.NodeData;
            BehaviorTreeData tree = SelectedTree;
            if (node == null || tree == null)
                return;

            EditorGUILayout.LabelField(BehaviorNodeDataRegistry.GetDisplayName(node.Type), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Guid", node.Guid ?? string.Empty);
                EditorGUILayout.TextField("Type", node.Type ?? string.Empty);
            }

            EditorGUI.BeginChangeCheck();
            switch (node)
            {
                case TargetInCastRangeBehaviorNodeData range:
                    range.RangePadding = EditorGUILayout.FloatField("Range Padding", range.RangePadding);
                    break;

                case MoveToTargetBehaviorNodeData move:
                    move.StopDistance = EditorGUILayout.FloatField("Stop Distance", move.StopDistance);
                    break;
            }
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
                _graphView.RefreshNode(selectedNode);
            }

            DrawChildOrderEditor(tree, node);
        }

        private void DrawChildOrderEditor(BehaviorTreeData tree, BehaviorNodeData node)
        {
            if (!BehaviorTreeGraphView.SupportsChildren(node))
                return;

            node.ChildGuids ??= new List<string>();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Children Order", EditorStyles.boldLabel);

            if (node.ChildGuids.Count == 0)
            {
                EditorGUILayout.HelpBox("No child connected.", MessageType.None);
                return;
            }

            for (int i = 0; i < node.ChildGuids.Count; i++)
            {
                string childGuid = node.ChildGuids[i];
                BehaviorNodeData childNode = tree.GetNode(childGuid);
                string childName = childNode == null ? "(Missing)" : BehaviorNodeDataRegistry.GetDisplayName(childNode.Type);

                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"{i + 1}. {childName}", GUILayout.ExpandWidth(true));

                GUI.enabled = i > 0;
                if (GUILayout.Button("Up", GUILayout.Width(44f)))
                {
                    SwapChildren(node, i, i - 1);
                    _graphView.MarkDirtyRepaint();
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    return;
                }

                GUI.enabled = i < node.ChildGuids.Count - 1;
                if (GUILayout.Button("Down", GUILayout.Width(52f)))
                {
                    SwapChildren(node, i, i + 1);
                    _graphView.MarkDirtyRepaint();
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    return;
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void SwapChildren(BehaviorNodeData node, int fromIndex, int toIndex)
        {
            string temp = node.ChildGuids[fromIndex];
            node.ChildGuids[fromIndex] = node.ChildGuids[toIndex];
            node.ChildGuids[toIndex] = temp;
            MarkDirty();
        }

        internal BehaviorTreeData SelectedTree =>
            _selectedIndex >= 0 && _selectedIndex < _rows.Count
                ? _rows[_selectedIndex]
                : null;

        internal void RebuildGraph()
        {
            _graphView?.BuildFromData(SelectedTree);
            _detailContainer?.MarkDirtyRepaint();
        }

        internal void MarkDirty()
        {
            _isDirty = true;
            UpdateStatus(_statusText);
            _detailContainer?.MarkDirtyRepaint();
        }

        internal void OnGraphDataChanged()
        {
            SyncNodePositionsFromGraph();
            MarkDirty();
        }

        internal void SyncNodePositionsFromGraph()
        {
            if (_graphView == null || SelectedTree == null)
                return;

            foreach (BehaviorTreeNodeView nodeView in _graphView.Query<BehaviorTreeNodeView>().ToList())
            {
                if (nodeView.NodeData == null)
                    continue;

                Rect rect = nodeView.GetPosition();
                nodeView.NodeData.EditorPosition = rect.position;
            }
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"Missing file: {DataPath}. It will be created on save.";
                UpdateStatus(_statusText);
                return;
            }

            try
            {
                string json = File.ReadAllText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                    _rows.AddRange(wrapper.Rows);

                for (int i = 0; i < _rows.Count; i++)
                    EnsureTreeValid(_rows[i]);

                NormalizeRowIds();
                _selectedIndex = _rows.Count > 0 ? Mathf.Clamp(_selectedIndex, 0, _rows.Count - 1) : -1;
                _statusText = $"Loaded {_rows.Count} tree(s) | {DataPath}";
                UpdateStatus(_statusText);
                RebuildGraph();
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                UpdateStatus(_statusText);
                Debug.LogError($"[BehaviorTreeEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            SyncNodePositionsFromGraph();

            string directory = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                NormalizeRowIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} tree(s) | {DataPath}";
                UpdateStatus(_statusText);
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                UpdateStatus(_statusText);
                Debug.LogError($"[BehaviorTreeEditor] Save error:\n{ex}");
            }
        }

        private void AddTree()
        {
            int id = _rows.Count + 1;
            BehaviorTreeData tree = CreateDefaultTree(id, $"BehaviorTree_{id}");
            _rows.Add(tree);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            MarkDirty();
            RebuildGraph();
        }

        private void DuplicateSelected()
        {
            BehaviorTreeData selected = SelectedTree;
            if (selected == null)
                return;

            string json = JsonConvert.SerializeObject(selected, JsonSettings);
            BehaviorTreeData copy = JsonConvert.DeserializeObject<BehaviorTreeData>(json, JsonSettings);
            if (copy == null)
                return;

            copy.Name = $"{GetTreeName(selected)}_Copy";
            EnsureTreeValid(copy, regenerateGuids: true);
            _rows.Add(copy);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            MarkDirty();
            RebuildGraph();
        }

        private void DeleteSelected()
        {
            BehaviorTreeData selected = SelectedTree;
            if (selected == null)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Behavior Tree",
                $"Delete '{GetTreeName(selected)}'?",
                "Delete",
                "Cancel");
            if (!confirmed)
                return;

            _rows.RemoveAt(_selectedIndex);
            _insertTexts.Remove(selected);
            NormalizeRowIds();
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            MarkDirty();
            RebuildGraph();
        }

        private void ValidateSelected()
        {
            BehaviorTreeData tree = SelectedTree;
            if (tree == null)
                return;

            List<string> errors = ValidateTree(tree);
            if (errors.Count == 0)
            {
                _statusText = $"Validation passed: {GetTreeName(tree)}";
            }
            else
            {
                _statusText = $"Validation failed: {errors[0]}";
                Debug.LogWarning("[BehaviorTreeEditor] Validation errors:\n" + string.Join("\n", errors));
            }
            UpdateStatus(_statusText);
        }

        private static BehaviorTreeData CreateDefaultTree(int id, string name)
        {
            RootBehaviorNodeData root = (RootBehaviorNodeData)BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Root);
            IdleBehaviorNodeData idle = (IdleBehaviorNodeData)BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Idle);

            root.EditorPosition = new Vector2(80f, 120f);
            idle.EditorPosition = new Vector2(360f, 120f);
            root.ChildGuids.Add(idle.Guid);

            return new BehaviorTreeData
            {
                Id = id,
                Name = name,
                Description = string.Empty,
                RootNodeGuid = root.Guid,
                Nodes = new List<BehaviorNodeData> { root, idle },
            };
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Id = i + 1;
        }

        private void MoveRowToInsertIndex(int fromIndex, int insertIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rows.Count)
                return;

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count - 1);
            if (fromIndex == insertIndex)
                return;

            BehaviorTreeData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);
            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            MarkDirty();
            RebuildGraph();
        }

        private void EnsureTreeValid(BehaviorTreeData tree, bool regenerateGuids = false)
        {
            if (tree == null)
                return;

            tree.Name ??= $"BehaviorTree_{tree.Id}";
            tree.Description ??= string.Empty;
            tree.Nodes ??= new List<BehaviorNodeData>();

            if (tree.Nodes.Count == 0)
            {
                BehaviorTreeData defaultTree = CreateDefaultTree(tree.Id, tree.Name);
                tree.RootNodeGuid = defaultTree.RootNodeGuid;
                tree.Nodes = defaultTree.Nodes;
                return;
            }

            var guidRemap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                if (node == null)
                {
                    tree.Nodes[i] = BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Idle);
                    node = tree.Nodes[i];
                }

                string previousGuid = node.Guid;
                node.Type = BehaviorNodeDataRegistry.ResolveTypeName(node);
                if (regenerateGuids || string.IsNullOrWhiteSpace(node.Guid))
                    node.Guid = Guid.NewGuid().ToString("N");
                if (!string.Equals(previousGuid, node.Guid, StringComparison.Ordinal))
                    guidRemap[previousGuid ?? string.Empty] = node.Guid;

                node.ChildGuids ??= new List<string>();
            }

            if (regenerateGuids)
            {
                for (int i = 0; i < tree.Nodes.Count; i++)
                {
                    BehaviorNodeData node = tree.Nodes[i];
                    for (int childIndex = 0; childIndex < node.ChildGuids.Count; childIndex++)
                    {
                        string childGuid = node.ChildGuids[childIndex];
                        if (guidRemap.TryGetValue(childGuid ?? string.Empty, out string newGuid))
                            node.ChildGuids[childIndex] = newGuid;
                    }
                }

                if (guidRemap.TryGetValue(tree.RootNodeGuid ?? string.Empty, out string newRootGuid))
                    tree.RootNodeGuid = newRootGuid;
            }

            var validGuids = new HashSet<string>(tree.Nodes.Select(node => node.Guid), StringComparer.Ordinal);
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                node.ChildGuids.RemoveAll(childGuid =>
                    string.IsNullOrWhiteSpace(childGuid) ||
                    string.Equals(childGuid, node.Guid, StringComparison.Ordinal) ||
                    !validGuids.Contains(childGuid));
            }

            BehaviorNodeData rootNode = tree.GetNode(tree.RootNodeGuid);
            if (rootNode == null || rootNode is not RootBehaviorNodeData)
            {
                RootBehaviorNodeData firstRoot = tree.Nodes.OfType<RootBehaviorNodeData>().FirstOrDefault();
                if (firstRoot == null)
                {
                    firstRoot = (RootBehaviorNodeData)BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Root);
                    firstRoot.EditorPosition = new Vector2(80f, 120f);
                    tree.Nodes.Insert(0, firstRoot);
                }

                tree.RootNodeGuid = firstRoot.Guid;
            }
        }

        private List<string> ValidateTree(BehaviorTreeData tree)
        {
            var errors = new List<string>();
            if (tree == null)
            {
                errors.Add("Tree is null.");
                return errors;
            }

            if (tree.Nodes == null || tree.Nodes.Count == 0)
                errors.Add("Tree has no nodes.");

            RootBehaviorNodeData[] roots = tree.Nodes?.OfType<RootBehaviorNodeData>().ToArray() ?? Array.Empty<RootBehaviorNodeData>();
            if (roots.Length != 1)
                errors.Add($"Tree must contain exactly one root node. Current: {roots.Length}");

            if (tree.GetNode(tree.RootNodeGuid) is not RootBehaviorNodeData)
                errors.Add("RootNodeGuid does not point to a root node.");

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                if (node == null)
                {
                    errors.Add($"Node #{i} is null.");
                    continue;
                }

                if (BehaviorTreeGraphView.SupportsChildren(node))
                {
                    int maxChildren = BehaviorTreeGraphView.GetMaxChildCount(node);
                    if (maxChildren >= 0 && node.ChildGuids.Count > maxChildren)
                        errors.Add($"{BehaviorNodeDataRegistry.GetDisplayName(node.Type)} exceeds child limit {maxChildren}.");
                }
                else if (node.ChildGuids.Count > 0)
                {
                    errors.Add($"{BehaviorNodeDataRegistry.GetDisplayName(node.Type)} should not have children.");
                }
            }

            return errors;
        }

        private void UpdateStatus(string text)
        {
            if (_isDirty && !string.IsNullOrWhiteSpace(text) && !text.Contains("*"))
                text += " *";

            _statusText = text;
            if (_statusLabel != null)
                _statusLabel.text = _statusText;
        }

        private static string GetTreeName(BehaviorTreeData tree)
        {
            if (!string.IsNullOrWhiteSpace(tree?.Name))
                return tree.Name;

            return "Unnamed Tree";
        }
    }

    public sealed class BehaviorTreeGraphView : GraphView
    {
        private readonly BehaviorTreeGraphWindow _window;
        private readonly Dictionary<string, BehaviorTreeNodeView> _nodeViews = new(StringComparer.Ordinal);

        public BehaviorTreeGraphView(BehaviorTreeGraphWindow window)
        {
            _window = window;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            MiniMap miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(10f, 30f, 180f, 120f));
            Add(miniMap);

            graphViewChanged = OnGraphViewChanged;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            var results = new List<Port>();
            foreach (Port port in ports)
            {
                if (port == startPort || port.node == startPort.node)
                    continue;
                if (port.direction == startPort.direction)
                    continue;
                results.Add(port);
            }
            return results;
        }

        public void BuildFromData(BehaviorTreeData tree)
        {
            ClearGraph();
            if (tree == null || tree.Nodes == null)
                return;

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                if (node == null)
                    continue;

                BehaviorTreeNodeView view = new BehaviorTreeNodeView(node);
                Rect rect = new Rect(node.EditorPosition, Vector2.zero);
                if (rect.position == Vector2.zero && i > 0)
                    rect.position = new Vector2(300f + i * 40f, 150f + i * 30f);
                view.SetPosition(rect);
                AddElement(view);
                _nodeViews[node.Guid] = view;
            }

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                if (node == null || !_nodeViews.TryGetValue(node.Guid, out BehaviorTreeNodeView source))
                    continue;

                node.ChildGuids ??= new List<string>();
                for (int childIndex = 0; childIndex < node.ChildGuids.Count; childIndex++)
                {
                    string childGuid = node.ChildGuids[childIndex];
                    if (string.IsNullOrWhiteSpace(childGuid))
                        continue;
                    if (!_nodeViews.TryGetValue(childGuid, out BehaviorTreeNodeView target))
                        continue;
                    if (source.OutputPort == null || target.InputPort == null)
                        continue;

                    Edge edge = source.OutputPort.ConnectTo(target.InputPort);
                    AddElement(edge);
                }
            }

            CleanupInvalidEdgesAndLinks(tree);
        }

        public void RefreshNode(BehaviorTreeNodeView nodeView)
        {
            nodeView?.RefreshDisplay();
        }

        public static bool SupportsChildren(BehaviorNodeData node)
        {
            return node is RootBehaviorNodeData or SelectorBehaviorNodeData or SequenceBehaviorNodeData;
        }

        public static int GetMaxChildCount(BehaviorNodeData node)
        {
            if (node is RootBehaviorNodeData)
                return 1;
            if (node is SelectorBehaviorNodeData or SequenceBehaviorNodeData)
                return -1;
            return 0;
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

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            BehaviorTreeData tree = _window.SelectedTree;
            if (tree == null)
                return change;

            if (change.edgesToCreate != null)
            {
                foreach (Edge edge in change.edgesToCreate)
                {
                    if (edge.output.node is not BehaviorTreeNodeView source ||
                        edge.input.node is not BehaviorTreeNodeView target)
                    {
                        continue;
                    }

                    source.NodeData.ChildGuids ??= new List<string>();
                    if (GetMaxChildCount(source.NodeData) == 1)
                        source.NodeData.ChildGuids.Clear();

                    if (!source.NodeData.ChildGuids.Contains(target.NodeData.Guid))
                        source.NodeData.ChildGuids.Add(target.NodeData.Guid);
                }
            }

            if (change.elementsToRemove != null)
            {
                foreach (GraphElement element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge when edge.output.node is BehaviorTreeNodeView source && edge.input.node is BehaviorTreeNodeView target:
                            source.NodeData.ChildGuids?.Remove(target.NodeData.Guid);
                            break;

                        case BehaviorTreeNodeView nodeView:
                            tree.Nodes.Remove(nodeView.NodeData);
                            _nodeViews.Remove(nodeView.NodeData.Guid);
                            for (int i = 0; i < tree.Nodes.Count; i++)
                                tree.Nodes[i]?.ChildGuids?.Remove(nodeView.NodeData.Guid);
                            if (string.Equals(tree.RootNodeGuid, nodeView.NodeData.Guid, StringComparison.Ordinal))
                                tree.RootNodeGuid = string.Empty;
                            break;
                    }
                }
            }

            if (change.movedElements is { Count: > 0 })
                _window.SyncNodePositionsFromGraph();

            CleanupInvalidEdgesAndLinks(tree);
            _window.OnGraphDataChanged();
            return change;
        }

        private void CleanupInvalidEdgesAndLinks(BehaviorTreeData tree)
        {
            if (tree == null)
                return;

            RemoveDanglingEdges();
            SyncChildLinksFromGraph(tree);
        }

        private void RemoveDanglingEdges()
        {
            var savedCallback = graphViewChanged;
            graphViewChanged = null;

            foreach (Edge edge in graphElements.ToList().OfType<Edge>())
            {
                bool hasValidOutput = edge.output?.node is BehaviorTreeNodeView;
                bool hasValidInput = edge.input?.node is BehaviorTreeNodeView;
                if (hasValidOutput && hasValidInput)
                    continue;

                RemoveElement(edge);
            }

            graphViewChanged = savedCallback;
        }

        private void SyncChildLinksFromGraph(BehaviorTreeData tree)
        {
            foreach (BehaviorNodeData node in tree.Nodes)
            {
                if (node == null)
                    continue;

                node.ChildGuids ??= new List<string>();
                if (!_nodeViews.TryGetValue(node.Guid, out BehaviorTreeNodeView sourceView) ||
                    !SupportsChildren(node) ||
                    sourceView.OutputPort == null)
                {
                    node.ChildGuids.Clear();
                    continue;
                }

                HashSet<string> connectedTargets = new(StringComparer.Ordinal);
                foreach (Edge edge in sourceView.OutputPort.connections)
                {
                    if (edge?.input?.node is not BehaviorTreeNodeView targetView)
                        continue;

                    connectedTargets.Add(targetView.NodeData.Guid);
                }

                node.ChildGuids.RemoveAll(childGuid =>
                    string.IsNullOrWhiteSpace(childGuid) || !connectedTargets.Contains(childGuid));

                foreach (Edge edge in sourceView.OutputPort.connections)
                {
                    if (edge?.input?.node is not BehaviorTreeNodeView targetView)
                        continue;

                    string targetGuid = targetView.NodeData.Guid;
                    if (!node.ChildGuids.Contains(targetGuid))
                        node.ChildGuids.Add(targetGuid);
                }
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPos = contentViewContainer.WorldToLocal(this.LocalToWorld(evt.localMousePosition));
            IReadOnlyList<FactoryTypeInfo> typeInfos = BehaviorNodeDataRegistry.TypeInfos;
            bool hasRoot = _window.SelectedTree?.Nodes?.OfType<RootBehaviorNodeData>().Any() == true;

            for (int i = 0; i < typeInfos.Count; i++)
            {
                FactoryTypeInfo typeInfo = typeInfos[i];
                if (hasRoot && string.Equals(typeInfo.Key, BehaviorNodeTypes.Root, StringComparison.Ordinal))
                    continue;

                FactoryTypeInfo capturedTypeInfo = typeInfo;
                evt.menu.AppendAction($"Add Node/{capturedTypeInfo.DisplayName}", _ =>
                {
                    BehaviorTreeData tree = _window.SelectedTree;
                    if (tree == null)
                        return;

                    BehaviorNodeData node = BehaviorNodeDataRegistry.Create(capturedTypeInfo.Key);
                    if (node == null)
                        return;

                    node.EditorPosition = graphPos;
                    tree.Nodes.Add(node);
                    if (node is RootBehaviorNodeData)
                        tree.RootNodeGuid = node.Guid;

                    _window.MarkDirty();
                    BuildFromData(tree);
                });
            }

            base.BuildContextualMenu(evt);
        }
    }

    public sealed class BehaviorTreeNodeView : Node
    {
        private readonly Label _summaryLabel;

        public BehaviorTreeNodeView(BehaviorNodeData nodeData)
        {
            NodeData = nodeData;
            title = BehaviorNodeDataRegistry.GetDisplayName(nodeData.Type);
            viewDataKey = nodeData.Guid;

            if (SupportsInput(nodeData))
            {
                InputPort = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
                InputPort.portName = string.Empty;
                mainContainer.Insert(0, CreatePortContainer(InputPort));
            }

            if (BehaviorTreeGraphView.SupportsChildren(nodeData))
            {
                Port.Capacity capacity = BehaviorTreeGraphView.GetMaxChildCount(nodeData) == 1
                    ? Port.Capacity.Single
                    : Port.Capacity.Multi;
                OutputPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, capacity, typeof(bool));
                OutputPort.portName = string.Empty;
            }

            _summaryLabel = new Label
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = new Color(0.8f, 0.8f, 0.8f, 1f),
                    whiteSpace = WhiteSpace.Normal,
                    marginTop = 4f,
                    marginLeft = 6f,
                    marginRight = 6f,
                }
            };
            extensionContainer.Add(_summaryLabel);
            if (OutputPort != null)
                mainContainer.Add(CreatePortContainer(OutputPort));

            Color color = GetNodeColor(nodeData);
            titleContainer.style.backgroundColor = color;
            style.minWidth = 180f;
            RefreshDisplay();
            RefreshPorts();
            RefreshExpandedState();
        }

        public BehaviorNodeData NodeData { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        public void RefreshDisplay()
        {
            title = BehaviorNodeDataRegistry.GetDisplayName(NodeData.Type);
            _summaryLabel.text = BehaviorNodeDataRegistry.GetSummary(NodeData);
        }

        private static VisualElement CreatePortContainer(Port port)
        {
            foreach (Label label in port.Query<Label>().ToList())
                label.style.display = DisplayStyle.None;

            port.style.marginLeft = 0f;
            port.style.marginRight = 0f;
            port.style.marginTop = 2f;
            port.style.marginBottom = 2f;
            port.style.minWidth = 12f;
            port.style.alignSelf = Align.Center;

            VisualElement container = new VisualElement
            {
                style =
                {
                    width = Length.Percent(100f),
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    alignSelf = Align.Stretch,
                }
            };
            container.Add(port);
            return container;
        }

        private static bool SupportsInput(BehaviorNodeData nodeData)
        {
            return nodeData is not RootBehaviorNodeData;
        }

        private static Color GetNodeColor(BehaviorNodeData nodeData)
        {
            return nodeData switch
            {
                RootBehaviorNodeData => new Color(0.24f, 0.45f, 0.70f, 1f),
                SelectorBehaviorNodeData => new Color(0.24f, 0.52f, 0.34f, 1f),
                SequenceBehaviorNodeData => new Color(0.18f, 0.45f, 0.28f, 1f),
                HasTargetBehaviorNodeData => new Color(0.70f, 0.42f, 0.18f, 1f),
                TargetInCastRangeBehaviorNodeData => new Color(0.70f, 0.50f, 0.18f, 1f),
                MoveToTargetBehaviorNodeData => new Color(0.47f, 0.32f, 0.69f, 1f),
                CastToTargetBehaviorNodeData => new Color(0.58f, 0.22f, 0.59f, 1f),
                AcquireNearestEnemyBehaviorNodeData => new Color(0.26f, 0.56f, 0.56f, 1f),
                IdleBehaviorNodeData => new Color(0.35f, 0.35f, 0.35f, 1f),
                _ => new Color(0.25f, 0.25f, 0.25f, 1f),
            };
        }
    }
}
