using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Editor.Unit
{
    // ═══════════════════════════════════════════════════════════════════
    //  Window
    // ═══════════════════════════════════════════════════════════════════

    public class StateMachineGraphWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/UnitDataTable.json";

        private List<UnitData> _rows = new();
        private int _selectedUnitIndex = -1;
        private bool _isDirty;

        private StateMachineGraphView _graphView;
        private Label _statusLabel;
        private PopupField<string> _unitPopup;
        private IMGUIContainer _detailImgui;

        private string[] _stateTypeNames   = Array.Empty<string>();
        private string[] _sourceTypeNames  = Array.Empty<string>();
        private string[] _compareTypeNames = Array.Empty<string>();

        private Dictionary<string, float[]> _nodePositions = new();

        private class TableWrapper { public List<UnitData> Rows = new(); }

        private static readonly JsonSerializerSettings s_json = new()
        {
            Formatting        = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        [MenuItem("Tools/State Machine/Visual Editor")]
        public static void Open()
        {
            var w = GetWindow<StateMachineGraphWindow>("SM Graph");
            w.minSize = new Vector2(960, 560);
            w.Show();
        }

        private void OnDisable() => SaveNodePositions();

        // ── UI Build ────────────────────────────────────────

        private void CreateGUI()
        {
            RefreshTypeArrays();
            LoadData();

            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            BuildToolbar(root);
            BuildMainArea(root);

            if (_rows.Count > 0)
            {
                _selectedUnitIndex = 0;
                RebuildGraph();
            }
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();

            var choices = _rows.Count > 0
                ? _rows.Select(r => $"[{r.Id}] {r.Name}").ToList()
                : new List<string> { "（无数据）" };

            _unitPopup = new PopupField<string>("单位", choices, 0) { style = { minWidth = 260 } };
            _unitPopup.RegisterValueChangedCallback(evt =>
            {
                int idx = _unitPopup.choices.IndexOf(evt.newValue);
                if (idx < 0 || idx >= _rows.Count || idx == _selectedUnitIndex) return;
                SaveNodePositions();
                _selectedUnitIndex = idx;
                RebuildGraph();
            });
            toolbar.Add(_unitPopup);

            toolbar.Add(MakeToolbarBtn("保存",     56, SaveData));
            toolbar.Add(MakeToolbarBtn("自动布局", 72, AutoLayout));

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            _statusLabel = new Label("就绪")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleRight,
                    marginRight    = 8,
                    color          = new Color(0.6f, 0.6f, 0.6f),
                }
            };
            toolbar.Add(_statusLabel);
            root.Add(toolbar);
        }

        private static ToolbarButton MakeToolbarBtn(string text, int width, Action click)
        {
            return new ToolbarButton(click) { text = text, style = { width = width } };
        }

        private void BuildMainArea(VisualElement root)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 }
            };

            _graphView = new StateMachineGraphView(this) { style = { flexGrow = 1 } };
            _graphView.RegisterCallback<MouseUpEvent>(_ =>
                _graphView.schedule.Execute(() => _detailImgui?.MarkDirtyRepaint()));
            _graphView.RegisterCallback<KeyUpEvent>(_ =>
                _graphView.schedule.Execute(() => _detailImgui?.MarkDirtyRepaint()));
            row.Add(_graphView);

            row.Add(new VisualElement
            {
                style = { width = 1, backgroundColor = new Color(0.1f, 0.1f, 0.1f) }
            });

            var detail = new VisualElement
            {
                style = { width = 280, minWidth = 240, backgroundColor = new Color(0.19f, 0.19f, 0.19f) }
            };
            detail.Add(new Label("转换详情")
            {
                style =
                {
                    fontSize                = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft             = 8,
                    paddingTop              = 6,
                    paddingBottom           = 4,
                }
            });
            detail.Add(new VisualElement
            {
                style = { height = 1, backgroundColor = new Color(0.35f, 0.35f, 0.35f), marginBottom = 2 }
            });

            _detailImgui = new IMGUIContainer(DrawDetailIMGUI) { style = { flexGrow = 1 } };
            detail.Add(_detailImgui);

            row.Add(detail);
            root.Add(row);
        }

        // ── Data I/O ────────────────────────────────────────

        private void LoadData()
        {
            _rows.Clear();
            _selectedUnitIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath)) { SetStatus($"未找到 {DataPath}"); return; }

            try
            {
                var wrapper = JsonConvert.DeserializeObject<TableWrapper>(
                    File.ReadAllText(DataPath), s_json);
                if (wrapper?.Rows != null) _rows = wrapper.Rows;
                foreach (var r in _rows) r.States ??= new List<UnitStateConfig>();
                SetStatus($"已加载 {_rows.Count} 条");
            }
            catch (Exception ex)
            {
                SetStatus($"加载失败：{ex.Message}");
                Debug.LogError($"[SMGraph] {ex}");
            }
        }

        private void SaveData()
        {
            SaveNodePositions();

            string dir = Path.GetDirectoryName(DataPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            try
            {
                File.WriteAllText(DataPath,
                    JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, s_json),
                    Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty = false;
                SetStatus($"已保存 {_rows.Count} 条");
            }
            catch (Exception ex)
            {
                SetStatus($"保存失败：{ex.Message}");
                Debug.LogError($"[SMGraph] {ex}");
            }
        }

        // ── Type reflection ─────────────────────────────────

        private void RefreshTypeArrays()
        {
            _stateTypeNames   = Collect(typeof(AUnitState),   true);
            _sourceTypeNames  = Collect(typeof(ISource),      false);
            _compareTypeNames = Collect(typeof(ICompareType), false);
        }

        private static string[] Collect(Type baseType, bool subclass)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => !t.IsAbstract && !t.IsInterface &&
                            (subclass ? t.IsSubclassOf(baseType) : baseType.IsAssignableFrom(t)))
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();
        }

        internal string[] StateTypeNames   => _stateTypeNames;
        internal string[] SourceTypeNames  => _sourceTypeNames;
        internal string[] CompareTypeNames => _compareTypeNames;

        // ── Graph ───────────────────────────────────────────

        internal UnitData SelectedUnit =>
            _selectedUnitIndex >= 0 && _selectedUnitIndex < _rows.Count
                ? _rows[_selectedUnitIndex]
                : null;

        internal void RebuildGraph()
        {
            if (_graphView == null || SelectedUnit == null) return;
            LoadNodePositions();
            _graphView.BuildFromData(SelectedUnit, _nodePositions);
        }

        // ── Node positions (EditorPrefs) ────────────────────

        private string PosKey => SelectedUnit != null ? $"SMGraph_Pos_{SelectedUnit.Id}" : null;

        private void LoadNodePositions()
        {
            _nodePositions.Clear();
            var k = PosKey;
            if (k == null) return;
            try
            {
                _nodePositions = JsonConvert.DeserializeObject<Dictionary<string, float[]>>(
                    EditorPrefs.GetString(k, "{}")) ?? new();
            }
            catch { _nodePositions = new(); }
        }

        internal void SaveNodePositions()
        {
            if (_graphView == null) return;
            var k = PosKey;
            if (k == null) return;

            _nodePositions.Clear();
            foreach (var n in _graphView.Query<StateNode>().ToList())
            {
                var p = n.GetPosition().position;
                _nodePositions[n.StateType] = new[] { p.x, p.y };
            }
            EditorPrefs.SetString(k, JsonConvert.SerializeObject(_nodePositions));
        }

        // ── Auto layout ────────────────────────────────────

        private void AutoLayout()
        {
            var nodes = _graphView?.Query<StateNode>().ToList();
            if (nodes == null || nodes.Count == 0) return;

            const float cx = 400f, cy = 280f;
            float r = Mathf.Max(180f, nodes.Count * 55f);

            for (int i = 0; i < nodes.Count; i++)
            {
                float a = 2f * Mathf.PI * i / nodes.Count - Mathf.PI / 2f;
                nodes[i].SetPosition(new Rect(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a), 0, 0));
            }

            SaveNodePositions();
            MarkDirty();
        }

        // ── Detail panel (IMGUI) ────────────────────────────

        private void DrawDetailIMGUI()
        {
            if (_graphView == null) return;

            var selectedEdge = _graphView.selection?.OfType<Edge>().FirstOrDefault();
            if (selectedEdge != null && _graphView.EdgeToTransition.TryGetValue(selectedEdge, out var trans))
            {
                DrawTransitionDetail(selectedEdge, trans);
                return;
            }

            var selectedNode = _graphView.selection?.OfType<StateNode>().FirstOrDefault();
            if (selectedNode != null)
            {
                DrawNodeDetail(selectedNode);
                return;
            }

            EditorGUILayout.HelpBox("选中连线 → 编辑转换条件\n选中节点 → 查看状态信息\n右键空白 → 添加新状态", MessageType.Info);
        }

        private void DrawNodeDetail(StateNode node)
        {
            EditorGUILayout.LabelField("状态节点", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("类型", node.StateType);
            EditorGUILayout.LabelField("初始状态", node.IsInitialState ? "是 ►" : "否");

            var unit = SelectedUnit;
            if (unit == null) return;
            var sc = unit.States.FirstOrDefault(s => s.StateType == node.StateType);
            if (sc == null) return;

            EditorGUILayout.LabelField("转出数", sc.Transitions.Count.ToString());

            int inCount = unit.States.Sum(s => s.Transitions.Count(t => t.TargetStateType == node.StateType));
            EditorGUILayout.LabelField("转入数", inCount.ToString());
        }

        private void DrawTransitionDetail(Edge edge, UnitTransitionConfig trans)
        {
            string src = (edge.output.node as StateNode)?.StateType ?? "?";
            string dst = (edge.input.node  as StateNode)?.StateType ?? "?";

            EditorGUILayout.LabelField($"{src}  →  {dst}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("＋ 添加条件", GUILayout.Height(22)))
            {
                trans.Conditions.Add(new ConditionConfig
                {
                    ConditionType = ConditionType.Necessary,
                    SourceType    = _sourceTypeNames.Length  > 0 ? _sourceTypeNames[0]  : "",
                    CompareType   = _compareTypeNames.Length > 0 ? _compareTypeNames[0] : "",
                });
                MarkDirty();
            }

            EditorGUILayout.Space(4);

            int removeIdx = -1;
            for (int i = 0; i < trans.Conditions.Count; i++)
            {
                if (!DrawCondition(trans.Conditions[i], i))
                    removeIdx = i;
            }

            if (removeIdx >= 0)
            {
                trans.Conditions.RemoveAt(removeIdx);
                MarkDirty();
            }

            if (trans.Conditions.Count == 0)
                EditorGUILayout.HelpBox("空条件 = 无条件转换（每帧触发）", MessageType.Warning);
        }

        private bool DrawCondition(ConditionConfig c, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField($"条件 #{index + 1}", EditorStyles.miniLabel);

            c.ConditionType = (ConditionType)EditorGUILayout.EnumPopup("类型", c.ConditionType);

            if (_sourceTypeNames.Length > 0)
            {
                int idx = Mathf.Max(0, Array.IndexOf(_sourceTypeNames, c.SourceType));
                idx            = EditorGUILayout.Popup("数据源", idx, _sourceTypeNames);
                c.SourceType   = _sourceTypeNames[idx];
            }
            else
            {
                c.SourceType = EditorGUILayout.TextField("数据源", c.SourceType);
            }

            if (_compareTypeNames.Length > 0)
            {
                int idx = Mathf.Max(0, Array.IndexOf(_compareTypeNames, c.CompareType));
                idx            = EditorGUILayout.Popup("比较", idx, _compareTypeNames);
                c.CompareType  = _compareTypeNames[idx];
            }
            else
            {
                c.CompareType = EditorGUILayout.TextField("比较", c.CompareType);
            }

            if (c.CompareType is "GreaterThan" or "LessThan" or "Equal")
                c.CompareValue = EditorGUILayout.FloatField("阈值", c.CompareValue);

            if (EditorGUI.EndChangeCheck()) MarkDirty();

            bool remove = false;
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("删除", GUILayout.Height(18))) remove = true;
            GUI.color = Color.white;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
            return !remove;
        }

        // ── Helpers ─────────────────────────────────────────

        internal void MarkDirty()
        {
            _isDirty = true;
            _detailImgui?.MarkDirtyRepaint();
            if (_statusLabel != null) _statusLabel.text = "未保存 *";
        }

        private void SetStatus(string t) { if (_statusLabel != null) _statusLabel.text = t; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GraphView
    // ═══════════════════════════════════════════════════════════════════

    public class StateMachineGraphView : GraphView
    {
        private readonly StateMachineGraphWindow _window;

        internal readonly Dictionary<Edge, UnitTransitionConfig> EdgeToTransition = new();
        private  readonly Dictionary<string, StateNode>          _stateNodes      = new();

        public StateMachineGraphView(StateMachineGraphWindow window)
        {
            _window = window;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            style.flexGrow = 1;

            var miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(10, 30, 180, 120));
            Add(miniMap);

            graphViewChanged = OnGraphViewChanged;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            var result = new List<Port>();
            foreach (var port in ports)
            {
                if (port.direction != startPort.direction && port.node != startPort.node)
                    result.Add(port);
            }
            return result;
        }

        // ── Build from data ─────────────────────────────────

        public void BuildFromData(UnitData data, Dictionary<string, float[]> positions)
        {
            ClearAll();
            if (data?.States == null) return;

            for (int i = 0; i < data.States.Count; i++)
            {
                var sc = data.States[i];
                Vector2 pos = positions.TryGetValue(sc.StateType, out var arr) && arr is { Length: >= 2 }
                    ? new Vector2(arr[0], arr[1])
                    : new Vector2(80 + i * 260, 220);

                var node = new StateNode(sc.StateType, i == 0);
                node.SetPosition(new Rect(pos, Vector2.zero));
                AddElement(node);
                _stateNodes[sc.StateType] = node;
            }

            foreach (var sc in data.States)
            {
                if (!_stateNodes.TryGetValue(sc.StateType, out var srcNode)) continue;
                foreach (var tr in sc.Transitions)
                {
                    if (!_stateNodes.TryGetValue(tr.TargetStateType, out var dstNode)) continue;
                    var edge = CreateTransitionEdge(srcNode, dstNode);
                    AddElement(edge);
                    EdgeToTransition[edge] = tr;
                }
            }
        }

        private void ClearAll()
        {
            var saved = graphViewChanged;
            graphViewChanged = null;

            foreach (var el in graphElements.ToList())
            {
                if (el is Edge or Node)
                    RemoveElement(el);
            }

            EdgeToTransition.Clear();
            _stateNodes.Clear();
            graphViewChanged = saved;
        }

        // ── Graph change callback ───────────────────────────

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            var unit = _window.SelectedUnit;
            if (unit == null) return change;

            if (change.edgesToCreate != null)
            {
                var replacementEdges = new List<Edge>();
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.output.node is not StateNode src ||
                        edge.input.node  is not StateNode dst)
                        continue;

                    var srcState = unit.States.FirstOrDefault(s => s.StateType == src.StateType);
                    if (srcState == null) continue;

                    var tr = new UnitTransitionConfig { TargetStateType = dst.StateType };
                    srcState.Transitions.Add(tr);
                    Edge customEdge = edge is StateTransitionEdge
                        ? edge
                        : CreateTransitionEdge(src, dst);
                    replacementEdges.Add(customEdge);
                    EdgeToTransition[customEdge] = tr;
                    _window.MarkDirty();
                }
                change.edgesToCreate = replacementEdges;
            }

            if (change.elementsToRemove != null)
            {
                foreach (var el in change.elementsToRemove)
                {
                    switch (el)
                    {
                        case Edge edge when EdgeToTransition.TryGetValue(edge, out var tr):
                        {
                            if (edge.output.node is StateNode src)
                            {
                                var srcState = unit.States.FirstOrDefault(s => s.StateType == src.StateType);
                                srcState?.Transitions.Remove(tr);
                            }
                            EdgeToTransition.Remove(edge);
                            _window.MarkDirty();
                            break;
                        }
                        case StateNode node:
                        {
                            unit.States.RemoveAll(s => s.StateType == node.StateType);
                            foreach (var s in unit.States)
                                s.Transitions.RemoveAll(t => t.TargetStateType == node.StateType);
                            _stateNodes.Remove(node.StateType);
                            _window.MarkDirty();
                            break;
                        }
                    }
                }
            }

            if (change.movedElements is { Count: > 0 })
                _window.MarkDirty();

            return change;
        }

        private static StateTransitionEdge CreateTransitionEdge(StateNode srcNode, StateNode dstNode)
        {
            var edge = new StateTransitionEdge
            {
                output = srcNode.OutputPort,
                input = dstNode.InputPort,
            };

            edge.output.Connect(edge);
            edge.input.Connect(edge);
            edge.MarkDirtyRepaint();
            return edge;
        }

        // ── Context menu ────────────────────────────────────

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            StateNode contextNode = FindStateNode(evt.target as VisualElement);
            if (contextNode != null)
            {
                bool addedConnectionAction = false;
                foreach (var pair in _stateNodes)
                {
                    if (pair.Value == contextNode)
                        continue;

                    string targetStateType = pair.Key;
                    bool exists = HasTransition(contextNode.StateType, targetStateType);
                    evt.menu.AppendAction(
                        $"Add Transition/{targetStateType}",
                        _ => TryAddTransition(contextNode.StateType, targetStateType),
                        _ => exists ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                    addedConnectionAction = true;
                }

                if (addedConnectionAction)
                    evt.menu.AppendSeparator();
            }

            Vector2 graphPos = contentViewContainer.WorldToLocal(
                this.LocalToWorld(evt.localMousePosition));

            var names = _window.StateTypeNames;
            if (names.Length > 0)
            {
                foreach (var typeName in names)
                {
                    if (_stateNodes.ContainsKey(typeName)) continue;

                    string captured    = typeName;
                    Vector2 capturedPos = graphPos;
                    evt.menu.AppendAction($"添加状态/{captured}", _ =>
                    {
                        var unit = _window.SelectedUnit;
                        if (unit == null) return;

                        unit.States.Add(new UnitStateConfig { StateType = captured });
                        var node = new StateNode(captured, unit.States.Count == 1);
                        node.SetPosition(new Rect(capturedPos, Vector2.zero));
                        AddElement(node);
                        _stateNodes[captured] = node;
                        _window.MarkDirty();
                    });
                }
                evt.menu.AppendSeparator();
            }

            base.BuildContextualMenu(evt);
        }

        private static StateNode FindStateNode(VisualElement element)
        {
            while (element != null)
            {
                if (element is StateNode node)
                    return node;
                element = element.parent;
            }

            return null;
        }

        private bool HasTransition(string sourceStateType, string targetStateType)
        {
            var unit = _window.SelectedUnit;
            if (unit?.States == null)
                return false;

            var sourceState = unit.States.FirstOrDefault(s => s.StateType == sourceStateType);
            return sourceState?.Transitions.Any(t => t.TargetStateType == targetStateType) == true;
        }

        private void TryAddTransition(string sourceStateType, string targetStateType)
        {
            var unit = _window.SelectedUnit;
            if (unit?.States == null)
                return;

            if (!_stateNodes.TryGetValue(sourceStateType, out var srcNode) ||
                !_stateNodes.TryGetValue(targetStateType, out var dstNode))
                return;

            var sourceState = unit.States.FirstOrDefault(s => s.StateType == sourceStateType);
            if (sourceState == null || sourceState.Transitions.Any(t => t.TargetStateType == targetStateType))
                return;

            var transition = new UnitTransitionConfig { TargetStateType = targetStateType };
            sourceState.Transitions.Add(transition);

            var edge = CreateTransitionEdge(srcNode, dstNode);
            AddElement(edge);
            EdgeToTransition[edge] = transition;
            _window.MarkDirty();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  State Node
    // ═══════════════════════════════════════════════════════════════════

    public sealed class StateNode : Node
    {
        public  string StateType      { get; }
        public  Port   InputPort      { get; }
        public  Port   OutputPort     { get; }
        public  bool   IsInitialState { get; }

        public StateNode(string stateType, bool isInitial)
        {
            StateType      = stateType;
            IsInitialState = isInitial;
            title          = isInitial ? $"► {stateType}" : stateType;
            tooltip        = isInitial ? "初始状态（States[0]）" : stateType;

            InputPort = Port.Create<Edge>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "进入";
            inputContainer.Add(InputPort);
            InputPort.portName = string.Empty;
            InputPort.style.display = DisplayStyle.None;

            OutputPort = Port.Create<Edge>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "转出";
            outputContainer.Add(OutputPort);
            OutputPort.portName = string.Empty;
            OutputPort.style.display = DisplayStyle.None;

            InputPort.style.minWidth = 12f;
            OutputPort.style.minWidth = 12f;

            if (isInitial)
            {
                titleContainer.style.backgroundColor = new Color(0.18f, 0.42f, 0.25f, 0.9f);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            RefreshConnectedEdges();
        }

        private void RefreshConnectedEdges()
        {
            if (InputPort != null)
            {
                foreach (Edge edge in InputPort.connections)
                    edge?.MarkDirtyRepaint();
            }

            if (OutputPort != null)
            {
                foreach (Edge edge in OutputPort.connections)
                    edge?.MarkDirtyRepaint();
            }
        }
    }

    public sealed class StateTransitionEdge : Edge
    {
        private static readonly Color NormalColor = new(0.78f, 0.78f, 0.78f, 0.95f);
        private static readonly Color SelectedColor = new(1f, 0.72f, 0.28f, 1f);
        private const float BidirectionalOffset = 10f;
        private const float HitDistance = 10f;

        public StateTransitionEdge()
        {
            edgeControl.style.opacity = 0f;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!TryGetSegment(out Vector2 from, out Vector2 to, out Vector2 normal))
                return;
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.01f)
                return;

            Vector2 direction = delta.normalized;
            Color color = selected ? SelectedColor : NormalColor;

            var painter = context.painter2D;
            painter.lineWidth = 2.2f;
            painter.strokeColor = color;
            painter.fillColor = color;

            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();

            float arrowLength = 14f;
            float arrowWidth = 6f;
            Vector2 arrowBase = to - direction * arrowLength;

            painter.BeginPath();
            painter.MoveTo(to);
            painter.LineTo(arrowBase + normal * arrowWidth);
            painter.LineTo(arrowBase - normal * arrowWidth);
            painter.ClosePath();
            painter.Fill();
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            if (!TryGetSegment(out Vector2 from, out Vector2 to, out _))
                return base.ContainsPoint(localPoint);

            return DistanceToSegment(localPoint, from, to) <= HitDistance;
        }

        private bool TryGetSegment(out Vector2 from, out Vector2 to, out Vector2 normal)
        {
            from = default;
            to = default;
            normal = default;

            if (output?.node is not StateNode srcNode || input?.node is not StateNode dstNode)
                return false;

            Vector2 fromCenter = this.WorldToLocal(srcNode.worldBound.center);
            Vector2 toCenter = this.WorldToLocal(dstNode.worldBound.center);
            Rect srcRect = new Rect(fromCenter - srcNode.worldBound.size * 0.5f, srcNode.worldBound.size);
            Rect dstRect = new Rect(toCenter - dstNode.worldBound.size * 0.5f, dstNode.worldBound.size);
            from = GetRectEdgePoint(srcRect, toCenter);
            to = GetRectEdgePoint(dstRect, fromCenter);

            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.01f)
                return false;

            Vector2 direction = delta.normalized;
            normal = new Vector2(-direction.y, direction.x);

            float signedOffset = GetSignedOffset(srcNode, dstNode);
            if (!Mathf.Approximately(signedOffset, 0f))
            {
                Vector2 offset = normal * signedOffset;
                from += offset;
                to += offset;
            }

            return true;
        }

        private float GetSignedOffset(StateNode srcNode, StateNode dstNode)
        {
            bool hasReverseEdge = dstNode.OutputPort != null &&
                                  dstNode.OutputPort.connections.Any(edge => edge?.input?.node == srcNode);
            if (!hasReverseEdge)
                return 0f;

            return BidirectionalOffset;
        }

        private static Vector2 GetRectEdgePoint(Rect rect, Vector2 targetPoint)
        {
            Vector2 center = rect.center;
            Vector2 delta = targetPoint - center;
            if (delta.sqrMagnitude < 0.0001f)
                return center;

            float tx = float.PositiveInfinity;
            float ty = float.PositiveInfinity;

            if (Mathf.Abs(delta.x) > 0.0001f)
                tx = delta.x > 0f ? (rect.xMax - center.x) / delta.x : (rect.xMin - center.x) / delta.x;
            if (Mathf.Abs(delta.y) > 0.0001f)
                ty = delta.y > 0f ? (rect.yMax - center.y) / delta.y : (rect.yMin - center.y) / delta.y;

            float t = Mathf.Min(
                tx > 0f ? tx : float.PositiveInfinity,
                ty > 0f ? ty : float.PositiveInfinity);

            if (float.IsInfinity(t))
                return center;

            return center + delta * t;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSq = segment.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return Vector2.Distance(point, start);

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSq);
            Vector2 projection = start + segment * t;
            return Vector2.Distance(point, projection);
        }
    }
}
