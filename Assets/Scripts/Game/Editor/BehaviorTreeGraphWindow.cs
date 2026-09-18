using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Game.Data;
using CrystalMagic.Core;
using CrystalMagic.Editor;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.Unit
{
    public class BehaviorTreeGraphWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/BehaviorTreeDataTable.json";
        private const string UnitPrefabDirectory = "Assets/Res/Prefab/Unit";
        private const string TreeDragDataKey = "CrystalMagic.BehaviorTree";
        private const float ListPanelWidth = 240f;
        private const float InspectorPanelMinWidth = 300f;

        private readonly List<BehaviorTreeData> _rows = new();
        private readonly List<UnitPrefabEntry> _unitEntries = new();
        private readonly List<RuntimeUnitEntry> _runtimeUnitEntries = new();
        private readonly Dictionary<int, string> _runtimePrefabNames = new();
        private readonly BehaviorTreeRuntimeDataInspector _runtimeDataInspector = new();
        private string _selectedPrefabPath;
        private int _selectedUnitDataId = -1;
        private Entity _selectedRuntimeEntity = Entity.Null;
        private BehaviorTreeDragData _pendingTreeDrag;
        private UnitSourceSchema _selectedSourceSchema;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private double _nextRuntimeUnitRefreshTime;

        private BehaviorTreeGraphView _graphView;
        private IMGUIContainer _listContainer;
        private IMGUIContainer _detailContainer;
        private Label _statusLabel;

        private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();
        private static readonly UnitSourceSchema s_emptySourceSchema = new UnitSourceSchemaBuilder().Build();
        private const double RuntimeUnitRefreshIntervalSeconds = 0.5d;
        private static bool IsRuntimeDebugEnabled => Application.isPlaying && DebugComponent.Instance.IsEnabled;

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new StateScriptVector2Converter(),
                new StateScriptUnitValueConverter(),
            },
        };

        private sealed class TableWrapper
        {
            public List<BehaviorTreeData> Rows = new();
        }

        private sealed class UnitPrefabEntry
        {
            public string AssetPath;
            public GameObject Prefab;
            public UnitData UnitData;

            public string DisplayName => Prefab != null && !string.IsNullOrWhiteSpace(Prefab.name)
                ? Prefab.name
                : Path.GetFileNameWithoutExtension(AssetPath);
        }

        private sealed class RuntimeUnitEntry
        {
            public Entity Entity;
            public int UnitDataId;
            public string PrefabName;

            public string DisplayName => $"{PrefabName} ({Entity})";
        }

        private sealed class BehaviorTreeDragData
        {
            public BehaviorTreeDragData(int sourceUnitDataId)
            {
                SourceUnitDataId = sourceUnitDataId;
            }

            public int SourceUnitDataId { get; }
        }

        [MenuItem("Tools/Data/Behavior Tree Visual Editor")]
        public static void Open()
        {
            BehaviorTreeGraphWindow window = GetWindow<BehaviorTreeGraphWindow>("Behavior Tree");
            window.minSize = new Vector2(1200f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _nextRuntimeUnitRefreshTime = 0d;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateGUI()
        {
            LoadData();

            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            BuildToolbar(root);
            BuildBody(root);

            if (SelectedTree != null)
                RebuildGraph();
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (!IsRuntimeDebugEnabled)
            {
                _runtimeUnitEntries.Clear();
                ClearRuntimeSelection();
                _runtimeDataInspector.Invalidate();
                _graphView?.RefreshRuntimeDebug(null);
                _listContainer?.MarkDirtyRepaint();
                Repaint();
                return;
            }

            if (EditorApplication.timeSinceStartup >= _nextRuntimeUnitRefreshTime)
                RefreshRuntimeUnitEntries();

            BehaviorTreeRuntime runtime = FindDebugRuntime();
            _runtimeDataInspector.Refresh(runtime, SelectedTree);
            _graphView?.RefreshRuntimeDebug(runtime);
            _listContainer?.MarkDirtyRepaint();
            _detailContainer?.MarkDirtyRepaint();
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ClearRuntimeSelection();
                _runtimeUnitEntries.Clear();
                _nextRuntimeUnitRefreshTime = 0d;
                RefreshRuntimeUnitEntries();
                RebuildGraph();
                Repaint();
                return;
            }

            if (change != PlayModeStateChange.ExitingPlayMode)
                return;

            _runtimeUnitEntries.Clear();
            ClearRuntimeSelection();
            _nextRuntimeUnitRefreshTime = 0d;
            if (SelectedUnitEntry == null)
                _selectedPrefabPath = _unitEntries.FirstOrDefault()?.AssetPath;
            _selectedUnitDataId = SelectedUnitEntry?.UnitData?.Id ?? -1;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(SelectedUnitEntry?.Prefab);
            RebuildGraph();
            Repaint();
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.Add(MakeToolbarButton(_isDirty ? "Save *" : "Save", 58f, SaveData));
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

            _listContainer = new IMGUIContainer(DrawListPanel)
            {
                style =
                {
                    width = ListPanelWidth,
                    minWidth = ListPanelWidth,
                }
            };
            body.Add(_listContainer);
            body.Add(CreateDivider());

            TwoPaneSplitView graphAndInspectorSplit = new(
                1,
                InspectorPanelMinWidth,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                style = { flexGrow = 1f },
            };

            _graphView = new BehaviorTreeGraphView(this)
            {
                style = { flexGrow = 1f },
            };
            _graphView.RegisterCallback<MouseUpEvent>(_ => NotifyGraphNodeSelected(_graphView.GetSelectedNodeGuid()));
            _graphView.RegisterCallback<KeyUpEvent>(_ => NotifyGraphNodeSelected(_graphView.GetSelectedNodeGuid()));
            graphAndInspectorSplit.Add(_graphView);

            var detailPanel = new VisualElement
            {
                style =
                {
                    minWidth = InspectorPanelMinWidth,
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

            graphAndInspectorSplit.Add(detailPanel);
            body.Add(graphAndInspectorSplit);
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
            bool showRuntimeDebug = IsRuntimeDebugEnabled;
            int unitCount = showRuntimeDebug ? _runtimeUnitEntries.Count : _unitEntries.Count;
            GUILayout.Label(showRuntimeDebug ? $"Runtime Units ({unitCount})" : $"Units ({unitCount})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            if (showRuntimeDebug)
            {
                DrawRuntimeUnitList();
            }
            else
            {
                for (int i = 0; i < _unitEntries.Count; i++)
                {
                    UnitPrefabEntry entry = _unitEntries[i];
                    bool isSelected = string.Equals(entry.AssetPath, _selectedPrefabPath, StringComparison.Ordinal);
                    string label = entry.UnitData == null
                        ? $"[No UnitData] {entry.DisplayName}"
                        : $"[{entry.UnitData.Id}] {entry.DisplayName}";
                    GUIStyle style = isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    Rect entryRect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.ExpandWidth(true));
                    HandleTreeDrop(entry, entryRect);
                    BeginTreeDrag(entry, entryRect);
                    if (GUI.Button(entryRect, label, style) && !isSelected)
                        SelectUnit(entry);
                }
            }

            if (showRuntimeDebug && _selectedRuntimeEntity == Entity.Null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("Select a live unit with a behavior tree to inspect its runtime data.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            UnitPrefabEntry selected = GetSelectedUnitEntry();
            if (selected == null && !showRuntimeDebug)
            {
                EditorGUILayout.HelpBox("Select a unit prefab.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(6f);
            if (selected != null)
                EditorGUILayout.LabelField(selected.DisplayName, EditorStyles.boldLabel);
            else
                EditorGUILayout.LabelField($"Unit Data [{SelectedUnitDataId}]", EditorStyles.boldLabel);

            if (SelectedTree == null)
                EditorGUILayout.HelpBox("No behavior tree data is assigned to this unit.", MessageType.Info);
            else
                EditorGUILayout.LabelField($"Tree: [{SelectedTree.Id}] {GetTreeName(SelectedTree)}", EditorStyles.miniLabel);

            if (showRuntimeDebug)
            {
                _runtimeDataInspector.Draw(FindDebugRuntime(), FindDebugDrawerContext(), SelectedTree);
            }
            else if (selected?.UnitData == null)
            {
                EditorGUILayout.HelpBox("This Prefab has no UnitData binding.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Drag a unit with behavior tree data onto another unit to copy it.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeUnitList()
        {
            if (_runtimeUnitEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No live unit with UnitBehaviorTreeComponent was found.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _runtimeUnitEntries.Count; i++)
            {
                RuntimeUnitEntry entry = _runtimeUnitEntries[i];
                GUIStyle style = entry.Entity == _selectedRuntimeEntity
                    ? EditorStyles.toolbarButton
                    : EditorStyles.miniButton;
                if (GUILayout.Button(entry.DisplayName, style, GUILayout.ExpandWidth(true)))
                    SelectRuntimeUnit(entry);
            }
        }

        private void DrawDetailPanel()
        {
            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            try
            {
                if (_graphView == null)
                    return;

                BehaviorTreeData tree = SelectedTree;
                if (tree == null)
                {
                    EditorGUILayout.HelpBox("Select a unit with behavior tree data.", MessageType.Info);
                    return;
                }

                DrawTreeSettings(tree);
                EditorGUILayout.Space(8f);

                BehaviorTreeNodeView selectedNode = _graphView.selection?.OfType<BehaviorTreeNodeView>().FirstOrDefault();
                if (selectedNode == null)
                {
                    EditorGUILayout.HelpBox("Select a node to edit its fields.", MessageType.Info);
                    return;
                }

                BehaviorNodeData node = selectedNode.NodeData;
                if (node == null)
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
                    case ParallelBehaviorNodeData parallel:
                        parallel.SuccessPolicy = (ParallelSuccessPolicy)EditorGUILayout.EnumPopup("Success Policy", parallel.SuccessPolicy);
                        parallel.FailurePolicy = (ParallelFailurePolicy)EditorGUILayout.EnumPopup("Failure Policy", parallel.FailurePolicy);
                        break;

                    case RepeaterBehaviorNodeData repeater:
                        repeater.ExecutionMode = (RepeaterExecutionMode)EditorGUILayout.EnumPopup("Execution Mode", repeater.ExecutionMode);
                        repeater.RepeatCount = EditorGUILayout.IntField("Repeat Count", repeater.RepeatCount);
                        break;

                    case CooldownBehaviorNodeData cooldown:
                        cooldown.CooldownSeconds = EditorGUILayout.FloatField("Cooldown Seconds", cooldown.CooldownSeconds);
                        break;

                    case TimeoutBehaviorNodeData timeout:
                        timeout.TimeoutSeconds = EditorGUILayout.FloatField("Timeout Seconds", timeout.TimeoutSeconds);
                        break;

                    case CheckBehaviorNodeData condition:
                        DrawConditionList(condition.Conditions);
                        break;

                    case HitCheckBehaviorNodeData hitCheck:
                        DrawHitCheckNode(hitCheck);
                        break;

                    case SetBehaviorNodeData set:
                        DrawSetNode(set);
                        break;

                    case WaitBehaviorNodeData wait:
                        wait.DurationSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Duration Seconds", wait.DurationSeconds));
                        break;

                    case MoveToBehaviorNodeData moveTo:
                        DrawMoveToNode(moveTo);
                        break;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    MarkDirty();
                    _graphView.RefreshNode(selectedNode);
                }

                DrawChildOrderEditor(tree, node);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTreeSettings(BehaviorTreeData tree)
        {
            EditorGUILayout.LabelField("Behavior Tree", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Id", tree.Id);
                EditorGUILayout.IntField("Unit Data Id", tree.UnitDataId);
                EditorGUILayout.TextField("Name", tree.Name ?? string.Empty);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Description");
            tree.Description = EditorGUILayout.TextArea(
                tree.Description ?? string.Empty,
                GUILayout.MinHeight(48f),
                GUILayout.MaxHeight(96f));
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
                Repaint();
            }
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

        private void DrawConditionList(List<ConditionConfig> conditions)
        {
            conditions ??= new List<ConditionConfig>();
            EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);

            for (int i = 0; i < conditions.Count; i++)
            {
                ConditionConfig condition = conditions[i] ?? new ConditionConfig();
                conditions[i] = condition;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Condition {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    conditions.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                condition.ConditionType = (ConditionType)EditorGUILayout.EnumPopup("Condition Type", condition.ConditionType);
                DrawCompareInputs(condition);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Condition"))
                conditions.Add(new ConditionConfig());
        }

        private void DrawCompareInputs(ConditionConfig condition)
        {
            List<string> compareKeys = s_expressionFactory.CompareTypeKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            if (compareKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("No compare types are registered.", MessageType.Error);
                return;
            }

            int selectedIndex = Mathf.Max(0, compareKeys.IndexOf(condition.CompareType));
            selectedIndex = EditorGUILayout.Popup("Compare", selectedIndex, compareKeys.ToArray());
            condition.CompareType = compareKeys[selectedIndex];
            if (!s_expressionFactory.TryCreateCompareType(condition.CompareType, out ICompareType compareType))
            {
                EditorGUILayout.HelpBox($"Unknown compare type: {condition.CompareType}", MessageType.Error);
                return;
            }

            EnsureExpressionCount(ref condition.Inputs, compareType.Parameters);
            for (int i = 0; i < compareType.Parameters.Count; i++)
                DrawValueExpression(condition.Inputs[i], compareType.Parameters[i], 0);
        }

        private void DrawSetNode(SetBehaviorNodeData node)
        {
            List<UnitSourceSetSchemaEntry> entries = SelectedSourceSchema.Sets
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No set accessors are registered.", MessageType.Warning);
                return;
            }

            string[] options = new string[entries.Count + 1];
            options[0] = "(Select accessor)";
            for (int i = 0; i < entries.Count; i++)
                options[i + 1] = entries[i].Key;

            int selectedIndex = entries.FindIndex(entry => string.Equals(entry.Key, node.SetKey, StringComparison.Ordinal)) + 1;
            selectedIndex = EditorGUILayout.Popup("Set", Mathf.Max(0, selectedIndex), options);
            if (selectedIndex <= 0)
            {
                if (!string.IsNullOrWhiteSpace(node.SetKey))
                    EditorGUILayout.HelpBox($"'{node.SetKey}' is not writable by this unit.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Choose a writable unit accessor.", MessageType.Info);
                return;
            }

            UnitSourceSetSchemaEntry selectedEntry = entries[selectedIndex - 1];
            node.SetKey = selectedEntry.Key;
            if (selectedEntry.RequiresKey)
            {
                node.Key = EditorGUILayout.TextField("Key", node.Key ?? string.Empty);
                if (string.IsNullOrWhiteSpace(node.Key))
                    EditorGUILayout.HelpBox($"Set '{selectedEntry.Key}' requires a key.", MessageType.Warning);
            }

            EnsureExpressionCount(ref node.Inputs, selectedEntry.Parameters);
            for (int i = 0; i < selectedEntry.Parameters.Count; i++)
                DrawValueExpression(node.Inputs[i], selectedEntry.Parameters[i], 0);
        }

        private void DrawHitCheckNode(HitCheckBehaviorNodeData node)
        {
            node.Target ??= new ValueExpression { Literal = UnitValue.FromEntity(Entity.Null) };
            DrawValueExpression(node.Target, new ComparatorParameterDefinition("Target", UnitValueCategory.Entity), 0);
            node.Center = EditorGUILayout.Vector2Field("Local Center", node.Center);
            node.Size = Vector2.Max(Vector2.zero, EditorGUILayout.Vector2Field("Size", node.Size));
            node.TargetPadding = Mathf.Max(0f, EditorGUILayout.FloatField("Target Padding", node.TargetPadding));
            EditorGUILayout.HelpBox("Local X and Y use world axes. Hit Check never rotates, but mirrors its X offset for left/right facing.", MessageType.None);
        }

        private void DrawMoveToNode(MoveToBehaviorNodeData node)
        {
            node.Conditions ??= new List<ConditionConfig>();
            DrawConditionList(node.Conditions);
            node.Destination ??= MoveToBehaviorNodeData.CreateDefaultDestination();
            node.StopDistance ??= MoveToBehaviorNodeData.CreateDefaultStopDistance();
            node.Speed ??= MoveToBehaviorNodeData.CreateDefaultSpeed();
            DrawValueExpression(node.Destination, new ComparatorParameterDefinition("Destination", UnitValueCategory.Float3), 0);
            DrawValueExpression(node.StopDistance, new ComparatorParameterDefinition("Stop Distance", UnitValueCategory.Number), 0);
            DrawValueExpression(node.Speed, new ComparatorParameterDefinition("Speed", UnitValueCategory.Number), 0);
        }

        private void DrawValueExpression(
            ValueExpression expression,
            ComparatorParameterDefinition parameter,
            int depth)
        {
            if (expression == null)
                return;

            EditorGUILayout.BeginVertical("box");
            string label = string.IsNullOrWhiteSpace(parameter.Name) ? "Input" : parameter.Name;
            EditorGUILayout.LabelField($"{label} ({parameter.Category})", EditorStyles.boldLabel);
            if (depth >= 6)
            {
                EditorGUILayout.HelpBox("Expression nesting is limited to 6 levels in the editor.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            expression.Kind = (ValueExpressionKind)EditorGUILayout.EnumPopup("Kind", expression.Kind);
            switch (expression.Kind)
            {
                case ValueExpressionKind.Literal:
                    DrawLiteralExpression(expression, parameter.Category);
                    break;

                case ValueExpressionKind.Getter:
                    DrawGetterExpression(expression, parameter.Category, depth + 1);
                    break;

                case ValueExpressionKind.Operation:
                    DrawOperationExpression(expression, parameter.Category, depth + 1);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawLiteralExpression(ValueExpression expression, UnitValueCategory expectedCategory)
        {
            UnitValueCategory category = expectedCategory == UnitValueCategory.Any
                ? GetConcreteCategory(expression.Literal.Category)
                : expectedCategory;
            if (expectedCategory == UnitValueCategory.Any)
            {
                UnitValueCategory nextCategory = (UnitValueCategory)EditorGUILayout.EnumPopup("Value Type", category);
                category = GetConcreteCategory(nextCategory);
            }

            if (expression.Literal.Category != category)
                expression.Literal = CreateDefaultLiteral(category);

            switch (category)
            {
                case UnitValueCategory.Bool:
                    expression.Literal = UnitValue.FromBool(EditorGUILayout.Toggle("Value", expression.Literal.Bool));
                    break;

                case UnitValueCategory.Number:
                    if (!expression.Literal.TryGetNumber(out float number))
                        number = 0f;
                    expression.Literal = UnitValue.FromFloat(EditorGUILayout.FloatField("Value", number));
                    break;

                case UnitValueCategory.Float2:
                    Vector2 float2Value = new(expression.Literal.Float2.x, expression.Literal.Float2.y);
                    float2Value = EditorGUILayout.Vector2Field("Value", float2Value);
                    expression.Literal = UnitValue.FromFloat2(new float2(float2Value.x, float2Value.y));
                    break;

                case UnitValueCategory.Float3:
                    Vector3 float3Value = new(expression.Literal.Float3.x, expression.Literal.Float3.y, expression.Literal.Float3.z);
                    float3Value = EditorGUILayout.Vector3Field("Value", float3Value);
                    expression.Literal = UnitValue.FromFloat3(new float3(float3Value.x, float3Value.y, float3Value.z));
                    break;

                case UnitValueCategory.Entity:
                    int entityIndex = EditorGUILayout.IntField("Entity Index", expression.Literal.Entity.Index);
                    int entityVersion = EditorGUILayout.IntField("Entity Version", expression.Literal.Entity.Version);
                    expression.Literal = UnitValue.FromEntity(new Entity { Index = entityIndex, Version = entityVersion });
                    break;

                case UnitValueCategory.String:
                    expression.Literal = UnitValue.FromString(EditorGUILayout.TextField("Value", expression.Literal.String ?? string.Empty));
                    break;
            }
        }

        private void DrawGetterExpression(ValueExpression expression, UnitValueCategory expectedCategory, int depth)
        {
            List<UnitSourceGetSchemaEntry> entries = SelectedSourceSchema.Gets
                .Where(entry => expectedCategory == UnitValueCategory.Any || entry.ReturnType == expectedCategory)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox($"No getter returns {expectedCategory}.", MessageType.Warning);
                return;
            }

            string[] options = new string[entries.Count + 1];
            options[0] = "(Select getter)";
            for (int i = 0; i < entries.Count; i++)
                options[i + 1] = entries[i].Key;

            int selectedIndex = entries.FindIndex(entry => string.Equals(entry.Key, expression.GetterKey, StringComparison.Ordinal)) + 1;
            selectedIndex = EditorGUILayout.Popup("Getter", Mathf.Max(0, selectedIndex), options);
            if (selectedIndex <= 0)
            {
                if (!string.IsNullOrWhiteSpace(expression.GetterKey))
                    EditorGUILayout.HelpBox($"'{expression.GetterKey}' is not available on this unit.", MessageType.Warning);
                return;
            }

            UnitSourceGetSchemaEntry selectedEntry = entries[selectedIndex - 1];
            expression.GetterKey = selectedEntry.Key;
            EnsureExpressionCount(ref expression.Inputs, selectedEntry.Parameters);
            for (int i = 0; i < selectedEntry.Parameters.Count; i++)
                DrawValueExpression(expression.Inputs[i], selectedEntry.Parameters[i], depth);
        }

        private void DrawOperationExpression(ValueExpression expression, UnitValueCategory expectedCategory, int depth)
        {
            List<IValueOperation> operations = s_expressionFactory.ValueOperationKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => s_expressionFactory.TryCreateValueOperation(key, out IValueOperation operation) ? operation : null)
                .Where(operation => operation != null &&
                                    (expectedCategory == UnitValueCategory.Any || operation.ResultCategory == expectedCategory))
                .ToList();
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox($"No operation returns {expectedCategory}.", MessageType.Warning);
                return;
            }

            int selectedIndex = Mathf.Max(0, operations.FindIndex(operation => string.Equals(
                GetOperationKey(operation), expression.OperationType, StringComparison.Ordinal)));
            selectedIndex = EditorGUILayout.Popup("Operation", selectedIndex, operations.Select(GetOperationKey).ToArray());
            IValueOperation selectedOperation = operations[selectedIndex];
            expression.OperationType = GetOperationKey(selectedOperation);
            EnsureExpressionCount(ref expression.Inputs, selectedOperation.Parameters);
            for (int i = 0; i < selectedOperation.Parameters.Count; i++)
                DrawValueExpression(expression.Inputs[i], selectedOperation.Parameters[i], depth);
        }

        private static string GetOperationKey(IValueOperation operation)
        {
            return s_expressionFactory.ValueOperationKeys.FirstOrDefault(key =>
                s_expressionFactory.TryCreateValueOperation(key, out IValueOperation candidate) &&
                candidate.GetType() == operation.GetType()) ?? string.Empty;
        }

        private static UnitValueCategory GetConcreteCategory(UnitValueCategory category)
        {
            return category is UnitValueCategory.Bool or UnitValueCategory.Number or UnitValueCategory.Float2 or
                UnitValueCategory.Float3 or UnitValueCategory.Entity or UnitValueCategory.String
                ? category
                : UnitValueCategory.Number;
        }

        private static UnitValue CreateDefaultLiteral(UnitValueCategory category)
        {
            return category switch
            {
                UnitValueCategory.Bool => UnitValue.FromBool(false),
                UnitValueCategory.Float2 => UnitValue.FromFloat2(float2.zero),
                UnitValueCategory.Float3 => UnitValue.FromFloat3(float3.zero),
                UnitValueCategory.Entity => UnitValue.FromEntity(Entity.Null),
                UnitValueCategory.String => UnitValue.FromString(string.Empty),
                _ => UnitValue.FromFloat(0f),
            };
        }

        private static void EnsureExpressionCount(
            ref List<ValueExpression> expressions,
            IReadOnlyList<ComparatorParameterDefinition> parameters)
        {
            expressions ??= new List<ValueExpression>();
            while (expressions.Count < parameters.Count)
            {
                expressions.Add(new ValueExpression
                {
                    Literal = CreateDefaultLiteral(parameters[expressions.Count].Category),
                });
            }

            if (expressions.Count > parameters.Count)
                expressions.RemoveRange(parameters.Count, expressions.Count - parameters.Count);

            for (int i = 0; i < expressions.Count; i++)
                expressions[i] ??= new ValueExpression { Literal = CreateDefaultLiteral(parameters[i].Category) };
        }

        private static string FormatParameters(IReadOnlyList<ComparatorParameterDefinition> parameters)
        {
            return string.Join(", ", parameters.Select(parameter => $"{parameter.Name}: {parameter.Category}"));
        }

        private static ComparatorFactory CreateExpressionFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }

        private void SwapChildren(BehaviorNodeData node, int fromIndex, int toIndex)
        {
            string temp = node.ChildGuids[fromIndex];
            node.ChildGuids[fromIndex] = node.ChildGuids[toIndex];
            node.ChildGuids[toIndex] = temp;
            MarkDirty();
        }

        private UnitPrefabEntry SelectedUnitEntry => _unitEntries.FirstOrDefault(entry =>
            string.Equals(entry.AssetPath, _selectedPrefabPath, StringComparison.Ordinal));

        private UnitPrefabEntry GetSelectedUnitEntry()
        {
            int unitDataId = SelectedUnitDataId;
            return unitDataId < 0
                ? null
                : _unitEntries.FirstOrDefault(entry => entry.UnitData != null && entry.UnitData.Id == unitDataId);
        }

        private int SelectedUnitDataId => _selectedRuntimeEntity != Entity.Null
            ? _selectedUnitDataId
            : SelectedUnitEntry?.UnitData?.Id ?? -1;

        internal BehaviorTreeData SelectedTree => GetTreeForUnit(SelectedUnitDataId);

        private UnitSourceSchema SelectedSourceSchema => _selectedSourceSchema ?? s_emptySourceSchema;

        private BehaviorTreeData GetTreeForUnit(int unitDataId)
        {
            return _rows.FirstOrDefault(row => row != null && row.UnitDataId == unitDataId);
        }

        internal void RebuildGraph()
        {
            _runtimeDataInspector.Invalidate();
            _graphView?.BuildFromData(SelectedTree);
            _detailContainer?.MarkDirtyRepaint();
            _listContainer?.MarkDirtyRepaint();
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

        internal void NotifyGraphNodeSelected(string nodeGuid)
        {
            _runtimeDataInspector.SetSelectedNode(nodeGuid);
            _detailContainer?.MarkDirtyRepaint();
            _listContainer?.MarkDirtyRepaint();
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

        private void SelectUnit(UnitPrefabEntry entry)
        {
            SyncNodePositionsFromGraph();
            ClearRuntimeSelection();
            _selectedPrefabPath = entry?.AssetPath;
            _selectedUnitDataId = entry?.UnitData?.Id ?? -1;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(entry?.Prefab);

            if (CreateEmptyTreeIfMissing(entry))
            {
                MarkDirty();
                UpdateStatus($"Created an empty behavior tree for {entry.DisplayName}. Save to persist it.");
            }

            RebuildGraph();
        }

        private bool CreateEmptyTreeIfMissing(UnitPrefabEntry entry)
        {
            if (entry?.UnitData == null || GetTreeForUnit(entry.UnitData.Id) != null)
                return false;

            RootBehaviorNodeData root = (RootBehaviorNodeData)BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Root);
            root.EditorPosition = new Vector2(80f, 120f);
            _rows.Add(new BehaviorTreeData
            {
                Id = GetNextTreeId(),
                UnitDataId = entry.UnitData.Id,
                Name = entry.DisplayName,
                Description = string.Empty,
                RootNodeGuid = root.Guid,
                Nodes = new List<BehaviorNodeData> { root },
            });
            return true;
        }

        private void BeginTreeDrag(UnitPrefabEntry entry, Rect entryRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && entryRect.Contains(currentEvent.mousePosition))
            {
                _pendingTreeDrag = entry?.UnitData != null && GetTreeForUnit(entry.UnitData.Id) != null
                    ? new BehaviorTreeDragData(entry.UnitData.Id)
                    : null;
                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                _pendingTreeDrag = null;
                return;
            }

            if (currentEvent.type != EventType.MouseDrag || currentEvent.button != 0 ||
                _pendingTreeDrag == null || entry?.UnitData == null ||
                entry.UnitData.Id != _pendingTreeDrag.SourceUnitDataId)
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(TreeDragDataKey, _pendingTreeDrag);
            DragAndDrop.StartDrag($"Copy behavior tree from {entry.DisplayName}");
            _pendingTreeDrag = null;
            currentEvent.Use();
        }

        private void HandleTreeDrop(UnitPrefabEntry targetEntry, Rect targetRect)
        {
            if (!TryGetDraggedTree(out BehaviorTreeDragData dragData) || targetEntry?.UnitData == null ||
                targetEntry.UnitData.Id == dragData.SourceUnitDataId || !targetRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    CopyTreeToUnit(dragData, targetEntry);
                    Event.current.Use();
                    break;
            }
        }

        private static bool TryGetDraggedTree(out BehaviorTreeDragData dragData)
        {
            dragData = DragAndDrop.GetGenericData(TreeDragDataKey) as BehaviorTreeDragData;
            return dragData != null;
        }

        private void CopyTreeToUnit(BehaviorTreeDragData dragData, UnitPrefabEntry targetEntry)
        {
            SyncNodePositionsFromGraph();
            BehaviorTreeData sourceTree = GetTreeForUnit(dragData.SourceUnitDataId);
            if (sourceTree == null)
            {
                UpdateStatus("The dragged unit no longer has behavior tree data.");
                return;
            }

            BehaviorTreeData targetTree = GetTreeForUnit(targetEntry.UnitData.Id);
            BehaviorTreeData copiedTree = CloneTree(sourceTree);
            copiedTree.Id = targetTree?.Id ?? GetNextTreeId();
            copiedTree.UnitDataId = targetEntry.UnitData.Id;
            copiedTree.Name = targetEntry.DisplayName;
            EnsureTreeValid(copiedTree, regenerateGuids: true);

            if (targetTree == null)
                _rows.Add(copiedTree);
            else
                _rows[_rows.IndexOf(targetTree)] = copiedTree;

            ClearRuntimeSelection();
            _selectedPrefabPath = targetEntry.AssetPath;
            _selectedUnitDataId = targetEntry.UnitData.Id;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(targetEntry.Prefab);
            MarkDirty();
            RebuildGraph();
            UpdateStatus($"Copied behavior tree from {sourceTree.Name} to {targetEntry.DisplayName}.");
        }

        private static BehaviorTreeData CloneTree(BehaviorTreeData sourceTree)
        {
            string json = JsonConvert.SerializeObject(sourceTree, JsonSettings);
            BehaviorTreeData copiedTree = JsonConvert.DeserializeObject<BehaviorTreeData>(json, JsonSettings);
            if (copiedTree == null)
                throw new InvalidDataException("Failed to clone behavior tree data.");

            return copiedTree;
        }

        private void LoadData()
        {
            _rows.Clear();
            _isDirty = false;

            try
            {
                if (File.Exists(DataPath))
                {
                    string json = DataFileUtility.ReadJsonText(DataPath);
                    TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                    if (wrapper?.Rows != null)
                        _rows.AddRange(wrapper.Rows);
                }

                for (int i = 0; i < _rows.Count; i++)
                    EnsureTreeValid(_rows[i]);

                EnsureStableTreeIds();
                RefreshUnitEntries();
                bool migrated = MigrateLegacyTreeBindings();
                if (!_unitEntries.Any(entry => string.Equals(entry.AssetPath, _selectedPrefabPath, StringComparison.Ordinal)))
                    _selectedPrefabPath = _unitEntries.FirstOrDefault()?.AssetPath;
                _selectedUnitDataId = SelectedUnitEntry?.UnitData?.Id ?? -1;
                _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(SelectedUnitEntry?.Prefab);

                if (migrated)
                {
                    SaveDataInternal(updateStatus: false);
                    _statusText = $"Loaded {_rows.Count} tree(s) and migrated unit bindings | {DataPath}";
                }
                else if (File.Exists(DataPath))
                {
                    _statusText = $"Loaded {_rows.Count} tree(s) | {DataPath}";
                }
                else
                {
                    _statusText = $"Loaded empty behavior tree data | {DataPath}";
                }

                if (CreateEmptyTreeIfMissing(SelectedUnitEntry))
                {
                    _isDirty = true;
                    _statusText = $"Created an empty behavior tree for {SelectedUnitEntry.DisplayName}. Save to persist it.";
                }

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
            SaveDataInternal(updateStatus: true);
        }

        private void SaveDataInternal(bool updateStatus)
        {
            SyncNodePositionsFromGraph();

            string directory = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                EnsureStableTreeIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                DataFileUtility.WriteJsonText(DataPath, json);
                AssetDatabase.Refresh();
                _isDirty = false;
                if (updateStatus)
                {
                    _statusText = $"Saved {_rows.Count} tree(s) | {DataPath}";
                    UpdateStatus(_statusText);
                }
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                UpdateStatus(_statusText);
                Debug.LogError($"[BehaviorTreeEditor] Save error:\n{ex}");
            }
        }

        private void EnsureStableTreeIds()
        {
            HashSet<int> usedIds = new();
            for (int i = 0; i < _rows.Count; i++)
            {
                BehaviorTreeData row = _rows[i];
                if (row == null)
                    continue;

                if (row.Id <= 0 || !usedIds.Add(row.Id))
                {
                    row.Id = GetNextTreeId(usedIds);
                    usedIds.Add(row.Id);
                }
            }
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
                RootBehaviorNodeData root = (RootBehaviorNodeData)BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Root);
                root.EditorPosition = new Vector2(80f, 120f);
                tree.RootNodeGuid = root.Guid;
                tree.Nodes = new List<BehaviorNodeData> { root };
                return;
            }

            var guidRemap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                BehaviorNodeData node = tree.Nodes[i];
                if (node == null)
                {
                    tree.Nodes[i] = BehaviorNodeDataRegistry.Create(BehaviorNodeTypes.Wait);
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

        private void RefreshUnitEntries()
        {
            _unitEntries.Clear();
            _runtimePrefabNames.Clear();
            if (!AssetDatabase.IsValidFolder(UnitPrefabDirectory))
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitPrefabDirectory });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<UnitBehaviorTreeAuthoring>() == null)
                    continue;

                UnitData unitData = EditorComponents.Data.Find<UnitData>(row =>
                    string.Equals(row.PrefabPath, path, StringComparison.Ordinal));
                _unitEntries.Add(new UnitPrefabEntry
                {
                    AssetPath = path,
                    Prefab = prefab,
                    UnitData = unitData,
                });

                if (unitData != null)
                    _runtimePrefabNames[unitData.Id] = prefab.name;
            }

            _unitEntries.Sort((left, right) =>
            {
                int leftId = left.UnitData?.Id ?? int.MaxValue;
                int rightId = right.UnitData?.Id ?? int.MaxValue;
                int idComparison = leftId.CompareTo(rightId);
                return idComparison != 0
                    ? idComparison
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            });
        }

        private void RefreshRuntimeUnitEntries()
        {
            _nextRuntimeUnitRefreshTime = EditorApplication.timeSinceStartup + RuntimeUnitRefreshIntervalSeconds;
            _runtimeUnitEntries.Clear();
            if (!IsRuntimeDebugEnabled)
            {
                ClearRuntimeSelection();
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitBehaviorTreeComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            bool selectedEntityFound = _selectedRuntimeEntity == Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                UnitBehaviorTreeComponent component = entityManager.GetComponentObject<UnitBehaviorTreeComponent>(entity);
                if (component == null)
                    continue;

                _runtimeUnitEntries.Add(new RuntimeUnitEntry
                {
                    Entity = entity,
                    UnitDataId = component.UnitDataId,
                    PrefabName = ResolveRuntimePrefabName(component.UnitDataId),
                });
                selectedEntityFound |= entity == _selectedRuntimeEntity;
            }

            _runtimeUnitEntries.Sort((left, right) =>
            {
                int nameComparison = string.Compare(left.PrefabName, right.PrefabName, StringComparison.Ordinal);
                return nameComparison != 0 ? nameComparison : left.Entity.Index.CompareTo(right.Entity.Index);
            });

            if (!selectedEntityFound)
            {
                ClearRuntimeSelection();
                RebuildGraph();
                UpdateStatus("Selected runtime unit no longer exists.");
            }
        }

        private string ResolveRuntimePrefabName(int unitDataId)
        {
            if (_runtimePrefabNames.TryGetValue(unitDataId, out string prefabName))
                return prefabName;

            UnitData unitData = EditorComponents.Data.Find<UnitData>(row => row.Id == unitDataId);
            string prefabPath = unitData?.PrefabPath;
            GameObject prefab = string.IsNullOrWhiteSpace(prefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            prefabName = prefab != null
                ? prefab.name
                : string.IsNullOrWhiteSpace(prefabPath)
                    ? "[Missing Prefab]"
                    : Path.GetFileNameWithoutExtension(prefabPath);
            _runtimePrefabNames[unitDataId] = prefabName;
            return prefabName;
        }

        private void SelectRuntimeUnit(RuntimeUnitEntry entry)
        {
            if (entry == null)
                return;

            SyncNodePositionsFromGraph();
            _selectedRuntimeEntity = entry.Entity;
            _selectedUnitDataId = entry.UnitDataId;
            UnitPrefabEntry prefabEntry = GetSelectedUnitEntry();
            _selectedPrefabPath = prefabEntry?.AssetPath;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(prefabEntry?.Prefab);
            RebuildGraph();
            UpdateStatus($"Debugging {entry.DisplayName}.");
        }

        private void ClearRuntimeSelection()
        {
            _selectedRuntimeEntity = Entity.Null;
            _selectedUnitDataId = -1;
        }

        private BehaviorTreeRuntime FindDebugRuntime()
        {
            if (!IsRuntimeDebugEnabled || _selectedRuntimeEntity == Entity.Null)
                return null;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return null;

            EntityManager entityManager = world.EntityManager;
            if (!entityManager.Exists(_selectedRuntimeEntity) ||
                !entityManager.HasComponent<UnitBehaviorTreeComponent>(_selectedRuntimeEntity))
            {
                return null;
            }

            return entityManager.GetComponentObject<UnitBehaviorTreeComponent>(_selectedRuntimeEntity)?.Runtime;
        }

        private UnitRuntimeDrawerContext FindDebugDrawerContext()
        {
            if (!IsRuntimeDebugEnabled || _selectedRuntimeEntity == Entity.Null)
                return null;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return null;

            EntityManager entityManager = world.EntityManager;
            if (!entityManager.Exists(_selectedRuntimeEntity))
                return null;

            UnitData unitData = EditorComponents.Data.Find<UnitData>(row => row.Id == SelectedUnitDataId);
            return new UnitRuntimeDrawerContext(
                entityManager,
                _selectedRuntimeEntity,
                ResolveRuntimePrefabName(SelectedUnitDataId),
                unitData);
        }

        private bool MigrateLegacyTreeBindings()
        {
            bool changed = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                BehaviorTreeData tree = _rows[i];
                if (tree == null || tree.UnitDataId >= 0)
                    continue;

                UnitPrefabEntry entry = _unitEntries.FirstOrDefault(candidate =>
                    candidate.UnitData != null &&
                    (string.Equals(candidate.DisplayName, tree.Name, StringComparison.Ordinal) ||
                    string.Equals(candidate.UnitData.Name, tree.Name, StringComparison.Ordinal)));
                if (entry == null)
                    continue;

                tree.UnitDataId = entry.UnitData.Id;
                if (string.IsNullOrWhiteSpace(tree.Name))
                    tree.Name = entry.DisplayName;
                changed = true;
            }

            if (changed)
                _isDirty = true;

            return changed;
        }

        private int GetNextTreeId()
        {
            return GetNextTreeId(new HashSet<int>(_rows.Where(row => row != null && row.Id > 0).Select(row => row.Id)));
        }

        private static int GetNextTreeId(HashSet<int> usedIds)
        {
            int candidate = 1;
            while (usedIds.Contains(candidate))
                candidate++;
            return candidate;
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

        public void RefreshRuntimeDebug(BehaviorTreeRuntime runtime)
        {
            foreach (BehaviorTreeNodeView view in _nodeViews.Values)
            {
                BehaviorNodeStatus status = default;
                bool hasStatus = runtime != null && runtime.TryGetDebugNodeStatus(view.NodeData.Guid, out status);
                view.RefreshRuntimeDebug(hasStatus, status);
            }
        }

        public string GetSelectedNodeGuid()
        {
            return selection?.OfType<BehaviorTreeNodeView>().FirstOrDefault()?.NodeData?.Guid;
        }

        public static bool SupportsChildren(BehaviorNodeData node)
        {
            return node is RootBehaviorNodeData or SelectorBehaviorNodeData or SequenceBehaviorNodeData or ParallelBehaviorNodeData
                or InverterBehaviorNodeData or SucceederBehaviorNodeData or FailerBehaviorNodeData
                or RepeaterBehaviorNodeData or UntilSuccessBehaviorNodeData or UntilFailureBehaviorNodeData
                or CooldownBehaviorNodeData or TimeoutBehaviorNodeData;
        }

        public static int GetMaxChildCount(BehaviorNodeData node)
        {
            if (node is RootBehaviorNodeData)
                return 1;
            if (node is SelectorBehaviorNodeData or SequenceBehaviorNodeData or ParallelBehaviorNodeData)
                return -1;
            if (node is InverterBehaviorNodeData or SucceederBehaviorNodeData or FailerBehaviorNodeData
                or RepeaterBehaviorNodeData or UntilSuccessBehaviorNodeData or UntilFailureBehaviorNodeData
                or CooldownBehaviorNodeData or TimeoutBehaviorNodeData)
                return 1;
            return 0;
        }

        private static string GetNodeCategory(string key)
        {
            return key switch
            {
                BehaviorNodeTypes.Root or
                BehaviorNodeTypes.Selector or
                BehaviorNodeTypes.Sequence or
                BehaviorNodeTypes.Parallel => "Composite",

                BehaviorNodeTypes.Inverter or
                BehaviorNodeTypes.Succeeder or
                BehaviorNodeTypes.Failer or
                BehaviorNodeTypes.Repeater or
                BehaviorNodeTypes.UntilSuccess or
                BehaviorNodeTypes.UntilFailure or
                BehaviorNodeTypes.Cooldown or
                BehaviorNodeTypes.Timeout => "Decorator",

                BehaviorNodeTypes.Check or BehaviorNodeTypes.HitCheck => "Condition",
                _ => "Action",
            };
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
                string category = GetNodeCategory(capturedTypeInfo.Key);
                evt.menu.AppendAction($"Add Node/{category}/{capturedTypeInfo.DisplayName}", _ =>
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

    public sealed class BehaviorTreeNodeView : TopBottomPortNode
    {
        private static readonly Color s_runningColor = new(0.20f, 0.55f, 1f, 0.95f);
        private static readonly Color s_successColor = new(0.20f, 0.72f, 0.34f, 0.95f);
        private static readonly Color s_failureColor = new(0.88f, 0.25f, 0.25f, 0.95f);

        public BehaviorTreeNodeView(BehaviorNodeData nodeData)
        {
            NodeData = nodeData;
            title = BehaviorNodeDataRegistry.GetDisplayName(nodeData.Type);
            viewDataKey = nodeData.Guid;

            if (SupportsInput(nodeData))
            {
                InputPort = CreateTopInput("Input", Port.Capacity.Single, typeof(bool));
            }

            if (BehaviorTreeGraphView.SupportsChildren(nodeData))
            {
                Port.Capacity capacity = BehaviorTreeGraphView.GetMaxChildCount(nodeData) == 1
                    ? Port.Capacity.Single
                    : Port.Capacity.Multi;
                OutputPort = CreateBottomOutput("Output", capacity, typeof(bool));
            }

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
        }

        public void RefreshRuntimeDebug(bool hasStatus, BehaviorNodeStatus status)
        {
            if (!hasStatus)
            {
                titleContainer.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                return;
            }

            StyleColor color = status switch
            {
                BehaviorNodeStatus.Running => new StyleColor(s_runningColor),
                BehaviorNodeStatus.Success => new StyleColor(s_successColor),
                BehaviorNodeStatus.Failure => new StyleColor(s_failureColor),
                _ => new StyleColor(StyleKeyword.Null),
            };
            titleContainer.style.backgroundColor = color;
        }

        private static bool SupportsInput(BehaviorNodeData nodeData)
        {
            return nodeData is not RootBehaviorNodeData;
        }

    }

    internal sealed class BehaviorTreeRuntimeDataInspector
    {
        private const int MaxExpressionDepth = 16;

        private static readonly UnitSourceSchema s_sourceSchema = UnitSourceSchemaFactory.CreateForAllSources();

        private readonly Dictionary<string, HashSet<Type>> _nodeComponentTypes = new(StringComparer.Ordinal);
        private BehaviorTreeRuntime _runtime;
        private BehaviorTreeData _tree;
        private string _selectedNodeGuid;

        public void Refresh(BehaviorTreeRuntime runtime, BehaviorTreeData tree)
        {
            if (ReferenceEquals(_runtime, runtime) && ReferenceEquals(_tree, tree))
                return;

            _runtime = runtime;
            _tree = tree;
            _nodeComponentTypes.Clear();
            if (tree?.Nodes == null)
                return;

            for (int i = 0; i < tree.Nodes.Count; i++)
                CollectNodeComponents(tree.Nodes[i]);
        }

        public void Invalidate()
        {
            _runtime = null;
            _tree = null;
            _nodeComponentTypes.Clear();
            _selectedNodeGuid = null;
        }

        public void SetSelectedNode(string nodeGuid)
        {
            _selectedNodeGuid = nodeGuid;
        }

        public void Draw(BehaviorTreeRuntime runtime, UnitRuntimeDrawerContext context, BehaviorTreeData tree)
        {
            Refresh(runtime, tree);
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Component Data", EditorStyles.boldLabel);
            if (context == null)
            {
                EditorGUILayout.HelpBox("Select a live unit to inspect runtime data.", MessageType.Info);
                return;
            }

            IReadOnlyList<IUnitRuntimeAttributeDrawer> drawers = UnitRuntimeAttributeDrawerFactory.GetDrawers();
            bool hasComponent = false;
            for (int i = 0; i < drawers.Count; i++)
            {
                IUnitRuntimeAttributeDrawer drawer = drawers[i];
                if (!drawer.CanDraw(context))
                    continue;

                hasComponent = true;
                bool isSelectedNodeComponent = drawer is IUnitRuntimeComponentDrawer componentDrawer &&
                                               IsSelectedNodeComponent(componentDrawer.ComponentType);
                DrawComponentDrawer(drawer, context, isSelectedNodeComponent);
            }

            if (!hasComponent)
                EditorGUILayout.HelpBox("This unit does not expose any registered component data.", MessageType.Info);
        }

        private void DrawComponentDrawer(
            IUnitRuntimeAttributeDrawer drawer,
            UnitRuntimeDrawerContext context,
            bool isSelectedNodeComponent)
        {
            Color originalColor = GUI.backgroundColor;
            if (isSelectedNodeComponent)
                GUI.backgroundColor = new Color(0.38f, 0.70f, 1f, 1f);

            EditorGUILayout.BeginVertical("box");
            using (new EditorGUI.DisabledScope(true))
                drawer.Draw(context);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = originalColor;
        }

        private bool IsSelectedNodeComponent(Type componentType)
        {
            return componentType != null &&
                   !string.IsNullOrWhiteSpace(_selectedNodeGuid) &&
                   _nodeComponentTypes.TryGetValue(_selectedNodeGuid, out HashSet<Type> componentTypes) &&
                   componentTypes.Contains(componentType);
        }

        private void CollectNodeComponents(BehaviorNodeData node)
        {
            switch (node)
            {
                case CheckBehaviorNodeData check:
                    CollectConditions(node.Guid, check.Conditions);
                    break;

                case HitCheckBehaviorNodeData hitCheck:
                    CollectExpression(node.Guid, hitCheck.Target, 0);
                    break;

                case SetBehaviorNodeData set:
                    TrackSource(node.Guid, set.SetKey);
                    if (set.Inputs == null)
                        return;

                    for (int i = 0; i < set.Inputs.Count; i++)
                        CollectExpression(node.Guid, set.Inputs[i], 0);
                    break;

                case MoveToBehaviorNodeData moveTo:
                    CollectConditions(node.Guid, moveTo.Conditions);
                    CollectExpression(node.Guid, moveTo.Destination, 0);
                    CollectExpression(node.Guid, moveTo.StopDistance, 0);
                    CollectExpression(node.Guid, moveTo.Speed, 0);
                    break;
            }
        }

        private void CollectConditions(string nodeGuid, List<ConditionConfig> conditions)
        {
            if (conditions == null)
                return;

            for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                ConditionConfig condition = conditions[conditionIndex];
                if (condition?.Inputs == null)
                    continue;

                for (int inputIndex = 0; inputIndex < condition.Inputs.Count; inputIndex++)
                    CollectExpression(nodeGuid, condition.Inputs[inputIndex], 0);
            }
        }

        private void CollectExpression(string nodeGuid, ValueExpression expression, int depth)
        {
            if (expression == null || depth >= MaxExpressionDepth)
                return;

            if (expression.Kind == ValueExpressionKind.Getter)
                TrackSource(nodeGuid, expression.GetterKey);

            if (expression.Inputs == null)
                return;

            for (int i = 0; i < expression.Inputs.Count; i++)
                CollectExpression(nodeGuid, expression.Inputs[i], depth + 1);
        }

        private void TrackSource(string nodeGuid, string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid) || string.IsNullOrWhiteSpace(sourceKey))
                return;

            if (s_sourceSchema.TryGet(sourceKey, out UnitSourceGetSchemaEntry getEntry))
            {
                TrackComponent(nodeGuid, getEntry.ComponentType);
                return;
            }

            if (s_sourceSchema.TryGet(sourceKey, out UnitSourceSetSchemaEntry setEntry))
                TrackComponent(nodeGuid, setEntry.ComponentType);
        }

        private void TrackComponent(string nodeGuid, Type componentType)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid) || componentType == null)
                return;

            if (!_nodeComponentTypes.TryGetValue(nodeGuid, out HashSet<Type> componentTypes))
            {
                componentTypes = new HashSet<Type>();
                _nodeComponentTypes.Add(nodeGuid, componentTypes);
            }

            componentTypes.Add(componentType);
        }
    }
}
