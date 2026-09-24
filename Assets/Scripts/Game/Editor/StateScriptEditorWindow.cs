using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor.Unit
{
    public sealed class StateScriptEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/StateScriptDataTable.json";
        private const string UnitPrefabDirectory = "Assets/Res/Prefab/Unit";
        private const string GraphDragDataKey = "CrystalMagic.StateScriptGraph";
        private const float ListPanelWidth = 270f;
        private const float InspectorPanelMinWidth = 300f;
        private const double RuntimeUnitRefreshIntervalSeconds = 0.5d;
        private const double RuntimeDebugRefreshIntervalSeconds = 0.2d;

        private readonly List<StateScriptData> _rows = new();
        private readonly List<UnitPrefabEntry> _unitEntries = new();
        private readonly List<RuntimeUnitEntry> _runtimeUnitEntries = new();
        private readonly Dictionary<int, string> _runtimePrefabNames = new();
        private readonly StateScriptRuntimeDataInspector _runtimeDataInspector = new();
        private int _selectedUnitDataId = -1;
        private Entity _selectedRuntimeEntity = Entity.Null;
        private string _selectedGraphGuid;
        private StateScriptGraphDragData _pendingGraphDrag;
        private UnitSourceSchema _selectedSourceSchema;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private Vector2 _listScroll;
        private double _nextRuntimeUnitRefreshTime;
        private double _nextRuntimeDebugRefreshTime;
        private bool _runtimeDebugActive;
        private StateScriptGraphDebugSnapshot _runtimeDebugSnapshot;

        private StateScriptGraphView _graphView;
        private IMGUIContainer _inspectorContainer;
        private Label _statusLabel;

        private static readonly UnitSourceSchema s_emptySourceSchema = new UnitSourceSchemaBuilder().Build();
        private static bool IsRuntimeDebugEnabled => Application.isPlaying && DebugComponent.Instance.IsEnabled;

        private sealed class TableWrapper
        {
            public List<StateScriptData> Rows = new();
        }

        private sealed class UnitPrefabEntry
        {
            public string AssetPath;
            public GameObject Prefab;
            public UnitData UnitData;

            public string DisplayName => UnitData?.Name ?? Prefab?.name ?? Path.GetFileNameWithoutExtension(AssetPath);
        }

        private sealed class RuntimeUnitEntry
        {
            public Entity Entity;
            public int UnitDataId;
            public string PrefabName;

            public string DisplayName => $"{PrefabName} ({Entity})";
        }

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new StateScriptVector2Converter(),
                new StateScriptUnitValueConverter(),
            },
        };

        [MenuItem("Tools/Data/State Script Visual Editor")]
        public static void Open()
        {
            StateScriptEditorWindow window = GetWindow<StateScriptEditorWindow>("State Script");
            window.minSize = new Vector2(1120f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _nextRuntimeUnitRefreshTime = 0d;
            _nextRuntimeDebugRefreshTime = 0d;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _runtimeDebugSnapshot = null;
            _runtimeDebugActive = false;
        }

        private void CreateGUI()
        {
            LoadData();

            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            BuildToolbar(root);
            BuildBody(root);

            if (SelectedGraph != null)
                RebuildGraph();
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (!IsRuntimeDebugEnabled)
            {
                if (!_runtimeDebugActive &&
                    _runtimeUnitEntries.Count == 0 &&
                    _runtimeDebugSnapshot == null)
                {
                    return;
                }

                _runtimeDebugActive = false;
                _runtimeUnitEntries.Clear();
                _selectedRuntimeEntity = Entity.Null;
                _runtimeDebugSnapshot = null;
                _runtimeDataInspector.Refresh(null);
                _graphView?.RefreshRuntimeDebug(null);
                Repaint();
                return;
            }

            _runtimeDebugActive = true;
            double currentTime = EditorApplication.timeSinceStartup;
            bool runtimeUnitsRefreshed = false;
            if (currentTime >= _nextRuntimeUnitRefreshTime)
            {
                RefreshRuntimeUnitEntries();
                runtimeUnitsRefreshed = true;
            }

            if (currentTime < _nextRuntimeDebugRefreshTime)
            {
                if (runtimeUnitsRefreshed)
                    Repaint();
                return;
            }

            _nextRuntimeDebugRefreshTime = currentTime + RuntimeDebugRefreshIntervalSeconds;

            if (_graphView == null || SelectedGraph == null)
            {
                bool hadRuntimeSnapshot = _runtimeDebugSnapshot != null;
                _runtimeDebugSnapshot = null;
                if (hadRuntimeSnapshot)
                {
                    _runtimeDataInspector.Refresh(null);
                    _graphView?.RefreshRuntimeDebug(null);
                }
                if (runtimeUnitsRefreshed || hadRuntimeSnapshot)
                    Repaint();
                return;
            }

            _runtimeDebugSnapshot = FindDebugRuntime();
            _runtimeDataInspector.Refresh(_runtimeDebugSnapshot);
            _graphView.RefreshRuntimeDebug(_runtimeDebugSnapshot);
            _inspectorContainer?.MarkDirtyRepaint();
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ClearRuntimeSelection();
                _runtimeUnitEntries.Clear();
                _nextRuntimeUnitRefreshTime = 0d;
                _nextRuntimeDebugRefreshTime = 0d;
                _runtimeDebugSnapshot = null;
                RefreshRuntimeUnitEntries();
                RebuildGraph();
                Repaint();
                return;
            }

            if (change != PlayModeStateChange.ExitingPlayMode)
                return;

            _runtimeUnitEntries.Clear();
            _selectedRuntimeEntity = Entity.Null;
            _nextRuntimeUnitRefreshTime = 0d;
            _nextRuntimeDebugRefreshTime = 0d;
            _runtimeDebugSnapshot = null;
            _runtimeDebugActive = false;
            if (GetSelectedUnitEntry() == null)
            {
                _selectedUnitDataId = _unitEntries.FirstOrDefault(entry => entry.UnitData != null)?.UnitData.Id ?? -1;
                _selectedGraphGuid = GetSelectedData()?.Graphs.FirstOrDefault(graph => graph != null)?.Guid;
            }

            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(GetSelectedUnitEntry()?.Prefab);
            RebuildGraph();
            Repaint();
        }

        private void BuildToolbar(VisualElement root)
        {
            Toolbar toolbar = new();
            toolbar.Add(CreateToolbarButton(_isDirty ? "Save *" : "Save", 58f, SaveData));
            toolbar.Add(new VisualElement { style = { flexGrow = 1f } });

            _statusLabel = new Label(_statusText)
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleRight,
                    marginRight = 8f,
                },
            };
            toolbar.Add(_statusLabel);
            root.Add(toolbar);
        }

        private void BuildBody(VisualElement root)
        {
            VisualElement body = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1f,
                },
            };

            body.Add(new IMGUIContainer(DrawListPanel)
            {
                style = { width = ListPanelWidth, minWidth = ListPanelWidth },
            });
            body.Add(CreateDivider());

            TwoPaneSplitView graphAndInspectorSplit = new(
                1,
                InspectorPanelMinWidth,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                style = { flexGrow = 1f },
            };

            _graphView = new StateScriptGraphView(this)
            {
                style = { flexGrow = 1f },
            };
            _graphView.RegisterCallback<MouseUpEvent>(_ => _inspectorContainer?.MarkDirtyRepaint());
            _graphView.RegisterCallback<KeyUpEvent>(_ => _inspectorContainer?.MarkDirtyRepaint());
            graphAndInspectorSplit.Add(_graphView);

            VisualElement inspectorPanel = new()
            {
                style =
                {
                    minWidth = InspectorPanelMinWidth,
                    backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f),
                },
            };
            inspectorPanel.Add(new Label("Inspector")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 8f,
                    paddingTop = 6f,
                    paddingBottom = 4f,
                },
            });
            inspectorPanel.Add(CreateDivider());
            _inspectorContainer = new IMGUIContainer(DrawInspectorPanel)
            {
                style = { flexGrow = 1f },
            };
            inspectorPanel.Add(_inspectorContainer);
            graphAndInspectorSplit.Add(inspectorPanel);
            body.Add(graphAndInspectorSplit);
            root.Add(body);
        }

        private void DrawListPanel()
        {
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.Space(6f);
            bool showRuntimeDebug = IsRuntimeDebugEnabled;
            if (showRuntimeDebug)
                DrawRuntimeUnitList();
            else
                DrawPrefabUnitList();

            if (showRuntimeDebug && _selectedRuntimeEntity == Entity.Null)
            {
                EditorGUILayout.HelpBox("Select a live unit with a StateScript component to inspect its graphs.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(10f);
            UnitPrefabEntry selectedEntry = GetSelectedUnitEntry();
            if (selectedEntry == null)
            {
                string message = showRuntimeDebug
                    ? "The selected runtime unit does not resolve to a UnitData prefab."
                    : "Select a unit prefab.";
                EditorGUILayout.HelpBox(message, MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField("Graphs", EditorStyles.boldLabel);
            StateScriptData data = GetSelectedData();
            if (data == null)
            {
                EditorGUILayout.HelpBox("No StateScriptData exists for this unit. Add Graph will create it.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < data.Graphs.Count; i++)
                {
                    StateScriptInstanceData graph = data.Graphs[i];
                    if (graph == null)
                        continue;

                    bool selected = string.Equals(graph.Guid, _selectedGraphGuid, StringComparison.Ordinal);
                    GUIStyle graphStyle = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    string graphName = string.IsNullOrWhiteSpace(graph.Name) ? "Unnamed Graph" : graph.Name;
                    Rect graphRect = GUILayoutUtility.GetRect(new GUIContent(graphName), graphStyle, GUILayout.ExpandWidth(true));
                    BeginGraphDrag(graph, graphRect);
                    if (GUI.Button(graphRect, graphName, graphStyle))
                    {
                        SelectGraph(graph.Guid);
                    }
                }

                StateScriptInstanceData selectedGraph = SelectedGraph;
                if (selectedGraph != null)
                {
                    EditorGUI.BeginChangeCheck();
                    selectedGraph.Name = EditorGUILayout.TextField("Graph Name", selectedGraph.Name ?? string.Empty);
                    if (EditorGUI.EndChangeCheck())
                        MarkDirty();

                    selectedGraph.ExecutionConditions ??= new List<ConditionConfig>();
                    EditorGUILayout.LabelField("Run Conditions", EditorStyles.boldLabel);
                    if (ConditionListEditor.Draw(
                            selectedGraph.ExecutionConditions,
                            $"StateScript.GraphRun.{_selectedUnitDataId}.{selectedGraph.Guid}",
                            _selectedSourceSchema ?? s_emptySourceSchema,
                            MarkDirty))
                    {
                        MarkDirty();
                    }
                }
            }

            if (showRuntimeDebug)
                _runtimeDataInspector.Draw(_runtimeDebugSnapshot);

            EditorGUILayout.Space(8f);
            if (selectedEntry.Prefab.GetComponent<UnitStateScriptAuthoring>() == null)
                EditorGUILayout.HelpBox("Attach UnitStateScriptAuthoring to this prefab before using its graph at runtime.", MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        private void DrawRuntimeUnitList()
        {
            EditorGUILayout.LabelField($"Runtime Units ({_runtimeUnitEntries.Count})", EditorStyles.boldLabel);
            if (_runtimeUnitEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No live unit with UnitStateScriptComponent was found.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _runtimeUnitEntries.Count; i++)
            {
                RuntimeUnitEntry entry = _runtimeUnitEntries[i];
                bool selected = entry.Entity == _selectedRuntimeEntity;
                GUIStyle style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(entry.DisplayName, style, GUILayout.ExpandWidth(true)))
                    SelectRuntimeUnit(entry);
            }
        }

        private void DrawPrefabUnitList()
        {
            EditorGUILayout.LabelField("Units", EditorStyles.boldLabel);

            for (int i = 0; i < _unitEntries.Count; i++)
            {
                UnitPrefabEntry entry = _unitEntries[i];
                bool selected = entry.UnitData != null && entry.UnitData.Id == _selectedUnitDataId;
                GUIStyle style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                Rect unitRect = GUILayoutUtility.GetRect(new GUIContent(entry.DisplayName), style, GUILayout.ExpandWidth(true));
                HandleGraphDrop(entry, unitRect);
                bool clicked = GUI.Button(unitRect, entry.DisplayName, style);
                if (!clicked)
                    continue;

                if (entry.UnitData == null)
                {
                    SetStatus($"UnitData is missing for prefab: {entry.DisplayName}");
                    continue;
                }

                SelectUnit(entry.UnitData.Id);
            }
        }

        private void DrawInspectorPanel()
        {
            StateScriptNodeInspector.Draw(_graphView?.GetSelectedNodeData(), SelectedSourceSchema, MarkDirty);
        }

        internal void NotifyGraphNodeSelected(string nodeGuid)
        {
            _runtimeDataInspector.SetSelectedNode(nodeGuid);
            _inspectorContainer?.MarkDirtyRepaint();
            Repaint();
        }

        private void LoadData()
        {
            _rows.Clear();
            try
            {
                if (File.Exists(DataPath))
                {
                    string json = DataFileUtility.ReadJsonText(DataPath);
                    TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                    if (wrapper?.Rows != null)
                        _rows.AddRange(wrapper.Rows.Where(row => row != null));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[StateScriptEditor] Failed to load data: {exception.Message}");
                SetStatus("Load failed. See console.");
            }

            for (int i = 0; i < _rows.Count; i++)
                _rows[i].EnsureValid();

            RefreshUnitEntries();
            if (Application.isPlaying)
            {
                ClearRuntimeSelection();
                RefreshRuntimeUnitEntries();
            }
            else if (GetSelectedUnitEntry() == null)
            {
                _selectedUnitDataId = _unitEntries.FirstOrDefault(entry => entry.UnitData != null)?.UnitData.Id ?? -1;
                StateScriptData selectedData = GetSelectedData();
                _selectedGraphGuid = selectedData?.Graphs.FirstOrDefault(graph => graph != null)?.Guid;
            }

            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(GetSelectedUnitEntry()?.Prefab);

            _isDirty = false;
            SetStatus("Loaded.");
            RebuildGraph();
        }

        private void SaveData()
        {
            try
            {
                // First collect the entire visible graph, then persist the complete table snapshot.
                _graphView?.SynchronizeToData(SelectedGraph);
                for (int i = 0; i < _rows.Count; i++)
                    _rows[i].EnsureValid();

                TableWrapper wrapper = new() { Rows = _rows };
                string json = JsonConvert.SerializeObject(wrapper, JsonSettings);
                ValidateSaveSnapshot(wrapper, json);

                DataFileUtility.WriteJsonText(DataPath, json);
                AssetDatabase.Refresh();
                _isDirty = false;
                SetStatus("Saved.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[StateScriptEditor] Failed to save data: {exception}");
                SetStatus("Save failed. See console.");
            }
        }

        private static void ValidateSaveSnapshot(TableWrapper source, string json)
        {
            TableWrapper snapshot = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
            if (snapshot?.Rows == null || snapshot.Rows.Count != source.Rows.Count)
                throw new InvalidDataException("StateScript table snapshot did not preserve every row.");

            for (int rowIndex = 0; rowIndex < source.Rows.Count; rowIndex++)
            {
                StateScriptData sourceRow = source.Rows[rowIndex];
                StateScriptData savedRow = snapshot.Rows[rowIndex];
                if (sourceRow == null || savedRow == null || sourceRow.Id != savedRow.Id ||
                    sourceRow.Graphs.Count != savedRow.Graphs.Count)
                {
                    throw new InvalidDataException("StateScript table snapshot did not preserve row data.");
                }

                for (int graphIndex = 0; graphIndex < sourceRow.Graphs.Count; graphIndex++)
                {
                    StateScriptInstanceData sourceGraph = sourceRow.Graphs[graphIndex];
                    StateScriptInstanceData savedGraph = savedRow.Graphs[graphIndex];
                    if (sourceGraph == null || savedGraph == null ||
                        sourceGraph.Nodes.Count != savedGraph.Nodes.Count ||
                        sourceGraph.Edges.Count != savedGraph.Edges.Count)
                    {
                        throw new InvalidDataException("StateScript table snapshot did not preserve graph data.");
                    }
                }
            }
        }

        private void AddGraph()
        {
            UnitPrefabEntry entry = GetSelectedUnitEntry();
            if (entry?.UnitData == null)
            {
                SetStatus("Select a unit before adding a graph.");
                return;
            }

            StateScriptData data = GetSelectedData();
            if (data == null)
            {
                data = new StateScriptData
                {
                    Id = entry.UnitData.Id,
                };
                _rows.Add(data);
            }

            StateScriptEntryNodeData entryNode = StateScriptNodeDataRegistry.Create("Entry") as StateScriptEntryNodeData;
            if (entryNode == null)
            {
                SetStatus("Entry node is not registered.");
                return;
            }

            entryNode.EditorPosition = new Vector2(120f, 180f);
            StateScriptInstanceData graph = new()
            {
                Guid = Guid.NewGuid().ToString("N"),
                Name = $"Graph {data.Graphs.Count + 1}",
                EntryNodeGuid = entryNode.Guid,
                Nodes = new List<StateScriptNodeData> { entryNode },
                Edges = new List<StateScriptEdgeData>(),
                ViewPosition = Vector2.zero,
                ViewScale = 1f,
            };
            data.Graphs.Add(graph);
            _selectedGraphGuid = graph.Guid;
            MarkDirty();
            RebuildGraph();
        }

        private void DeleteSelectedGraph()
        {
            StateScriptData data = GetSelectedData();
            StateScriptInstanceData graph = SelectedGraph;
            if (data == null || graph == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete StateScript Graph", $"Delete graph '{graph.Name}'?", "Delete", "Cancel"))
                return;

            data.Graphs.Remove(graph);
            _selectedGraphGuid = data.Graphs.FirstOrDefault(candidate => candidate != null)?.Guid;
            MarkDirty();
            RebuildGraph();
        }

        private void BeginGraphDrag(StateScriptInstanceData graph, Rect graphRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && graphRect.Contains(currentEvent.mousePosition))
            {
                _pendingGraphDrag = new StateScriptGraphDragData(_selectedUnitDataId, graph.Guid);
                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                _pendingGraphDrag = null;
                return;
            }

            if (currentEvent.type != EventType.MouseDrag || currentEvent.button != 0 ||
                _pendingGraphDrag == null ||
                !string.Equals(_pendingGraphDrag.SourceGraphGuid, graph.Guid, StringComparison.Ordinal))
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(GraphDragDataKey, _pendingGraphDrag);
            DragAndDrop.StartDrag($"Copy StateScript Graph '{graph.Name}'");
            _pendingGraphDrag = null;
            currentEvent.Use();
        }

        private void HandleGraphDrop(UnitPrefabEntry targetEntry, Rect targetRect)
        {
            if (!TryGetDraggedGraph(out StateScriptGraphDragData dragData) || targetEntry?.UnitData == null ||
                !targetRect.Contains(Event.current.mousePosition))
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
                    CopyGraphToUnit(dragData, targetEntry);
                    Event.current.Use();
                    break;
            }
        }

        private bool TryGetDraggedGraph(out StateScriptGraphDragData dragData)
        {
            dragData = DragAndDrop.GetGenericData(GraphDragDataKey) as StateScriptGraphDragData;
            return dragData != null;
        }

        private void CopyGraphToUnit(StateScriptGraphDragData dragData, UnitPrefabEntry targetEntry)
        {
            _graphView?.SynchronizeToData(SelectedGraph);
            StateScriptData sourceData = _rows.FirstOrDefault(row => row.Id == dragData.SourceUnitDataId);
            StateScriptInstanceData sourceGraph = sourceData?.Graphs?.FirstOrDefault(graph =>
                graph != null && string.Equals(graph.Guid, dragData.SourceGraphGuid, StringComparison.Ordinal));
            if (sourceGraph == null)
            {
                SetStatus("The dragged graph no longer exists.");
                return;
            }

            UnitSourceSchema targetSchema = UnitSourceSchemaFactory.CreateForPrefab(targetEntry.Prefab);
            StateScriptGraphCopyResult result = StateScriptGraphCopyUtility.CreateCopy(sourceGraph, targetEntry.Prefab, targetSchema);
            StateScriptInstanceData copiedGraph = result.Graph;
            StateScriptData targetData = GetOrCreateData(targetEntry.UnitData.Id);
            copiedGraph.Name = GetUniqueGraphName(targetData, copiedGraph.Name);
            targetData.Graphs.Add(copiedGraph);

            _selectedUnitDataId = targetEntry.UnitData.Id;
            _selectedSourceSchema = targetSchema;
            _selectedGraphGuid = copiedGraph.Guid;
            MarkDirty();
            RebuildGraph();
            SetStatus(result.ResetNodeCount == 0
                ? $"Copied graph '{copiedGraph.Name}' to {targetEntry.DisplayName}."
                : $"Copied graph '{copiedGraph.Name}' to {targetEntry.DisplayName}; reset {result.ResetNodeCount} unsupported node(s).");
        }

        private StateScriptData GetOrCreateData(int unitDataId)
        {
            StateScriptData data = _rows.FirstOrDefault(row => row.Id == unitDataId);
            if (data != null)
                return data;

            data = new StateScriptData { Id = unitDataId };
            _rows.Add(data);
            return data;
        }

        private static string GetUniqueGraphName(StateScriptData targetData, string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? "Copied Graph" : sourceName;
            if (targetData.Graphs.All(graph => graph == null || !string.Equals(graph.Name, baseName, StringComparison.Ordinal)))
                return baseName;

            for (int copyIndex = 2; ; copyIndex++)
            {
                string candidate = $"{baseName} {copyIndex}";
                if (targetData.Graphs.All(graph => graph == null || !string.Equals(graph.Name, candidate, StringComparison.Ordinal)))
                    return candidate;
            }
        }

        private void ValidateSelectedGraph()
        {
            List<string> errors = StateScriptGraphValidator.Validate(SelectedGraph);
            SetStatus(errors.Count == 0 ? "Graph is valid." : string.Join(" | ", errors));
        }

        internal StateScriptInstanceData SelectedGraph
        {
            get
            {
                StateScriptData data = GetSelectedData();
                if (data?.Graphs == null)
                    return null;

                return data.Graphs.FirstOrDefault(graph => graph != null && string.Equals(graph.Guid, _selectedGraphGuid, StringComparison.Ordinal));
            }
        }

        internal void MarkDirty()
        {
            _isDirty = true;
            _runtimeDataInspector.Invalidate();
            _graphView?.RefreshExecutionTargetBadges();
            SetStatus("Modified.");
            _inspectorContainer?.MarkDirtyRepaint();
        }

        internal void RebuildGraph()
        {
            _runtimeDebugSnapshot = null;
            _nextRuntimeDebugRefreshTime = 0d;
            _runtimeDataInspector.Invalidate();
            if (_graphView == null)
                return;

            _graphView.BuildFromData(SelectedGraph);
            _inspectorContainer?.MarkDirtyRepaint();
        }

        internal void SaveGraphViewTransform()
        {
            if (_graphView != null && _graphView.SaveViewTransform(SelectedGraph))
                MarkDirty();
        }

        internal void SelectGraph(string graphGuid)
        {
            SaveGraphViewTransform();
            _selectedGraphGuid = graphGuid;
            RebuildGraph();
        }

        internal void NotifyGraphChanged()
        {
            MarkDirty();
            _inspectorContainer?.MarkDirtyRepaint();
        }

        private void SelectUnit(int unitDataId)
        {
            SaveGraphViewTransform();
            _selectedRuntimeEntity = Entity.Null;
            _selectedUnitDataId = unitDataId;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(GetSelectedUnitEntry()?.Prefab);
            StateScriptData data = GetSelectedData();
            _selectedGraphGuid = data?.Graphs.FirstOrDefault(graph => graph != null)?.Guid;
            RebuildGraph();
        }

        private void SelectRuntimeUnit(RuntimeUnitEntry entry)
        {
            if (entry == null)
                return;

            SaveGraphViewTransform();
            _selectedRuntimeEntity = entry.Entity;
            _selectedUnitDataId = entry.UnitDataId;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(GetSelectedUnitEntry()?.Prefab);

            StateScriptData data = GetSelectedData();
            bool hasSelectedGraph = data?.Graphs.Any(graph => graph != null && string.Equals(graph.Guid, _selectedGraphGuid, StringComparison.Ordinal)) == true;
            if (!hasSelectedGraph)
                _selectedGraphGuid = data?.Graphs.FirstOrDefault(graph => graph != null)?.Guid;

            RebuildGraph();
            SetStatus($"Debugging {entry.DisplayName}.");
        }

        private StateScriptData GetSelectedData()
        {
            return _rows.FirstOrDefault(row => row.Id == _selectedUnitDataId);
        }

        private UnitPrefabEntry GetSelectedUnitEntry()
        {
            return _unitEntries.FirstOrDefault(entry => entry.UnitData != null && entry.UnitData.Id == _selectedUnitDataId);
        }

        private UnitSourceSchema SelectedSourceSchema => _selectedSourceSchema ?? s_emptySourceSchema;

        private void RefreshUnitEntries()
        {
            _unitEntries.Clear();
            _runtimePrefabNames.Clear();
            if (!AssetDatabase.IsValidFolder(UnitPrefabDirectory))
                return;

            Dictionary<string, UnitData> unitDataByPrefabPath = new(StringComparer.Ordinal);
            foreach (UnitData unitData in EditorComponents.Data.FindAll<UnitData>(static _ => true))
            {
                if (unitData != null && !string.IsNullOrWhiteSpace(unitData.PrefabPath))
                    unitDataByPrefabPath.TryAdd(unitData.PrefabPath, unitData);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitPrefabDirectory });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                unitDataByPrefabPath.TryGetValue(path, out UnitData unitData);
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
                _selectedRuntimeEntity = Entity.Null;
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitStateScriptComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            bool selectedEntityFound = _selectedRuntimeEntity == Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                UnitStateScriptComponent component = entityManager.GetComponentData<UnitStateScriptComponent>(entity);

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
                SetStatus("Selected runtime unit no longer exists.");
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

        private void ClearRuntimeSelection()
        {
            _selectedRuntimeEntity = Entity.Null;
            _selectedUnitDataId = -1;
            _selectedGraphGuid = null;
            _selectedSourceSchema = s_emptySourceSchema;
            _runtimeDebugSnapshot = null;
            _nextRuntimeDebugRefreshTime = 0d;
        }

        private StateScriptGraphDebugSnapshot FindDebugRuntime()
        {
            if (!IsRuntimeDebugEnabled || _selectedRuntimeEntity == Entity.Null)
                return null;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return null;

            EntityManager entityManager = world.EntityManager;
            if (!entityManager.Exists(_selectedRuntimeEntity) ||
                !entityManager.HasComponent<UnitStateScriptComponent>(_selectedRuntimeEntity))
            {
                return null;
            }

            UnitStateScriptComponent component = entityManager.GetComponentData<UnitStateScriptComponent>(_selectedRuntimeEntity);
            if (entityManager.HasComponent<UnitInitializationPendingTag>(_selectedRuntimeEntity) ||
                !entityManager.HasBuffer<StateScriptGraphStateElement>(_selectedRuntimeEntity) ||
                !entityManager.HasBuffer<StateScriptNodeStateElement>(_selectedRuntimeEntity))
                return null;

            StateScriptData data = _rows.FirstOrDefault(row => row.Id == component.UnitDataId);
            int graphIndex = data?.Graphs?.FindIndex(graph =>
                graph != null && string.Equals(graph.Guid, _selectedGraphGuid, StringComparison.Ordinal)) ?? -1;
            DynamicBuffer<StateScriptGraphStateElement> graphStates =
                entityManager.GetBuffer<StateScriptGraphStateElement>(_selectedRuntimeEntity);
            if (graphIndex < 0 || graphIndex >= graphStates.Length)
                return null;

            StateScriptInstanceData graphData = data.Graphs[graphIndex];
            DynamicBuffer<StateScriptNodeStateElement> nodeStates =
                entityManager.GetBuffer<StateScriptNodeStateElement>(_selectedRuntimeEntity);
            int stateStart = graphStates[graphIndex].NodeStateStart;
            if (stateStart < 0 || stateStart + graphData.Nodes.Count > nodeStates.Length)
                return null;

            Dictionary<string, StateScriptNodeDebugState> debugStates = new(StringComparer.Ordinal);
            for (int nodeIndex = 0; nodeIndex < graphData.Nodes.Count; nodeIndex++)
            {
                StateScriptNodeData node = graphData.Nodes[nodeIndex];
                if (node == null || string.IsNullOrWhiteSpace(node.Guid))
                    continue;
                StateScriptNodeStateElement state = nodeStates[stateStart + nodeIndex];
                debugStates[node.Guid] = new StateScriptNodeDebugState(
                    node is StateStateScriptNodeData,
                    state.Status,
                    state.LastPulseTick);
            }
            return new StateScriptGraphDebugSnapshot(
                entityManager,
                _selectedRuntimeEntity,
                graphData,
                debugStates);
        }

        private static ToolbarButton CreateToolbarButton(string text, float width, Action action)
        {
            return new ToolbarButton(action) { text = text, style = { width = width } };
        }

        private sealed class StateScriptGraphDragData
        {
            public StateScriptGraphDragData(int sourceUnitDataId, string sourceGraphGuid)
            {
                SourceUnitDataId = sourceUnitDataId;
                SourceGraphGuid = sourceGraphGuid ?? string.Empty;
            }

            public int SourceUnitDataId { get; }
            public string SourceGraphGuid { get; }
        }

        private static VisualElement CreateDivider()
        {
            return new VisualElement
            {
                style =
                {
                    width = 1f,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f),
                },
            };
        }

        private void SetStatus(string text)
        {
            _statusText = text ?? string.Empty;
            if (_statusLabel != null)
                _statusLabel.text = _statusText + (_isDirty ? " *" : string.Empty);
        }
    }

    internal readonly struct StateScriptGraphCopyResult
    {
        public StateScriptGraphCopyResult(StateScriptInstanceData graph, int resetNodeCount)
        {
            Graph = graph;
            ResetNodeCount = resetNodeCount;
        }

        public StateScriptInstanceData Graph { get; }
        public int ResetNodeCount { get; }
    }

    internal static class StateScriptGraphCopyUtility
    {
        private const int MaxExpressionDepth = 32;

        private static readonly ComparatorFactory s_comparatorFactory = CreateComparatorFactory();
        private static readonly JsonSerializerSettings s_jsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new StateScriptVector2Converter(),
                new StateScriptUnitValueConverter(),
            },
        };

        public static StateScriptGraphCopyResult CreateCopy(
            StateScriptInstanceData source,
            GameObject targetPrefab,
            UnitSourceSchema targetSchema)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            StateScriptInstanceData copy = Clone(source);
            copy.EnsureValid();
            AssignNewIdentifiers(copy);
            int resetNodeCount = ResetUnsupportedNodeData(copy, targetPrefab, targetSchema);
            copy.EnsureValid();
            return new StateScriptGraphCopyResult(copy, resetNodeCount);
        }

        private static StateScriptInstanceData Clone(StateScriptInstanceData source)
        {
            string json = JsonConvert.SerializeObject(source, s_jsonSettings);
            StateScriptInstanceData copy = JsonConvert.DeserializeObject<StateScriptInstanceData>(json, s_jsonSettings);
            if (copy == null)
                throw new InvalidOperationException("Failed to clone StateScript graph data.");

            return copy;
        }

        private static void AssignNewIdentifiers(StateScriptInstanceData graph)
        {
            Dictionary<string, string> nodeGuidMap = new(StringComparer.Ordinal);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                StateScriptNodeData node = graph.Nodes[i];
                if (node == null)
                    continue;

                string oldGuid = node.Guid ?? string.Empty;
                string newGuid = Guid.NewGuid().ToString("N");
                nodeGuidMap[oldGuid] = newGuid;
                node.Guid = newGuid;
            }

            if (nodeGuidMap.TryGetValue(graph.EntryNodeGuid ?? string.Empty, out string entryGuid))
                graph.EntryNodeGuid = entryGuid;

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                StateScriptEdgeData edge = graph.Edges[i];
                if (edge == null)
                    continue;

                if (nodeGuidMap.TryGetValue(edge.OutputNodeGuid ?? string.Empty, out string outputGuid))
                    edge.OutputNodeGuid = outputGuid;
                if (nodeGuidMap.TryGetValue(edge.InputNodeGuid ?? string.Empty, out string inputGuid))
                    edge.InputNodeGuid = inputGuid;
            }

            graph.Guid = Guid.NewGuid().ToString("N");
        }

        private static int ResetUnsupportedNodeData(
            StateScriptInstanceData graph,
            GameObject targetPrefab,
            UnitSourceSchema targetSchema)
        {
            int resetNodeCount = 0;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                StateScriptNodeData node = graph.Nodes[i];
                if (node == null || IsNodeDataSupported(node, targetPrefab, targetSchema))
                    continue;

                StateScriptNodeData resetNode = StateScriptNodeDataRegistry.Create(node.Type, assignGuid: false);
                if (resetNode == null)
                    continue;

                resetNode.Guid = node.Guid;
                resetNode.EditorPosition = node.EditorPosition;
                resetNode.ExecutionTargets = node.ExecutionTargets;
                graph.Nodes[i] = resetNode;
                resetNodeCount++;
            }

            return resetNodeCount;
        }

        private static bool IsNodeDataSupported(
            StateScriptNodeData node,
            GameObject targetPrefab,
            UnitSourceSchema targetSchema)
        {
            switch (node)
            {
                case SetValueStateScriptNodeData setValue:
                    return IsSetValueSupported(setValue, targetSchema);
                case CompareStateScriptNodeData compare:
                    return IsConditionSupported(compare.Condition, targetSchema);
                case MonitorStateScriptNodeData monitor:
                    return IsConditionSupported(monitor.Condition, targetSchema);
                case RequestSkillActionNodeData requestSkill:
                    return IsRequestSkillSupported(requestSkill, targetPrefab, targetSchema);
                case QueryUnitsActionNodeData queryUnits:
                    return IsQueryUnitsSupported(queryUnits, targetPrefab, targetSchema);
                case ExecuteEffectActionNodeData executeEffect:
                    return IsExecuteEffectSupported(executeEffect, targetSchema);
                case TimerStateScriptNodeData timer:
                    return IsTimerSupported(timer, targetSchema);
                case NumberMonitorStateScriptNodeData numberMonitor:
                    return IsNumberMonitorSupported(numberMonitor, targetSchema);
                default:
                    // Nodes without source or component dependencies can retain their data unchanged.
                    return true;
            }
        }

        private static bool IsSetValueSupported(SetValueStateScriptNodeData setValue, UnitSourceSchema schema)
        {
            if (setValue == null || schema == null ||
                !schema.TryGet(setValue.SetterKey, out UnitSourceSetSchemaEntry setter) ||
                (setter.RequiresKey && string.IsNullOrWhiteSpace(setValue.Key)))
            {
                return false;
            }

            List<ValueExpression> values = setValue.GetOrCreateValues(setter.Parameters.Count);
            if (values.Count != setter.Parameters.Count)
                return false;

            for (int i = 0; i < setter.Parameters.Count; i++)
            {
                if (!TryGetExpressionCategory(values[i], schema, 0, out UnitValueCategory valueCategory) ||
                    !setter.Parameters[i].Accepts(valueCategory))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsConditionSupported(ConditionConfig condition, UnitSourceSchema schema)
        {
            if (condition == null || schema == null ||
                !s_comparatorFactory.TryCreateCompareType(condition.CompareType, out ICompareType compareType))
            {
                return false;
            }

            return HasCompatibleInputs(condition.Inputs, compareType.Parameters, schema, 0);
        }

        private static bool IsRequestSkillSupported(
            RequestSkillActionNodeData requestSkill,
            GameObject targetPrefab,
            UnitSourceSchema targetSchema)
        {
            if (requestSkill == null || targetPrefab == null ||
                targetPrefab.GetComponentInChildren<UnitSkillReleaseAuthoring>(true) == null)
            {
                return false;
            }

            return TryGetExpressionCategory(requestSkill.SkillId, targetSchema, 0, out UnitValueCategory category) &&
                   category == UnitValueCategory.Number;
        }

        private static bool IsTimerSupported(TimerStateScriptNodeData timer, UnitSourceSchema schema)
        {
            return timer != null &&
                   TryGetExpressionCategory(timer.Duration, schema, 0, out UnitValueCategory category) &&
                   category == UnitValueCategory.Number;
        }

        private static bool IsQueryUnitsSupported(
            QueryUnitsActionNodeData query,
            GameObject targetPrefab,
            UnitSourceSchema schema)
        {
            return query != null &&
                   targetPrefab != null &&
                   targetPrefab.GetComponentInChildren<UnitVariableAuthoring>(true) != null &&
                   TryGetExpressionCategory(query.Center, schema, 0, out UnitValueCategory centerCategory) &&
                   centerCategory == UnitValueCategory.Float3 &&
                   TryGetExpressionCategory(query.Direction, schema, 0, out UnitValueCategory directionCategory) &&
                   directionCategory == UnitValueCategory.Float2 &&
                   TryGetExpressionCategory(query.Size, schema, 0, out UnitValueCategory sizeCategory) &&
                   sizeCategory == UnitValueCategory.Float2 &&
                   TryGetExpressionCategory(query.Radius, schema, 0, out UnitValueCategory radiusCategory) &&
                   radiusCategory == UnitValueCategory.Number &&
                   TryGetExpressionCategory(query.Angle, schema, 0, out UnitValueCategory angleCategory) &&
                   angleCategory == UnitValueCategory.Number;
        }

        private static bool IsExecuteEffectSupported(
            ExecuteEffectActionNodeData executeEffect,
            UnitSourceSchema schema)
        {
            return executeEffect != null &&
                   TryGetExpressionCategory(executeEffect.TargetEntity, schema, 0, out UnitValueCategory targetCategory) &&
                   targetCategory == UnitValueCategory.Entity &&
                   TryGetExpressionCategory(executeEffect.OtherEntity, schema, 0, out UnitValueCategory otherCategory) &&
                   otherCategory == UnitValueCategory.Entity &&
                   TryGetExpressionCategory(executeEffect.Position, schema, 0, out UnitValueCategory positionCategory) &&
                   positionCategory == UnitValueCategory.Float3 &&
                   TryGetExpressionCategory(executeEffect.TriggerValue, schema, 0, out UnitValueCategory triggerCategory) &&
                   triggerCategory == UnitValueCategory.Number &&
                   TryGetExpressionCategory(executeEffect.SourceSkillId, schema, 0, out UnitValueCategory skillCategory) &&
                   skillCategory == UnitValueCategory.Number;
        }

        private static bool IsNumberMonitorSupported(NumberMonitorStateScriptNodeData numberMonitor, UnitSourceSchema schema)
        {
            return numberMonitor != null &&
                   TryGetExpressionCategory(numberMonitor.Value, schema, 0, out UnitValueCategory category) &&
                   category == UnitValueCategory.Number;
        }

        private static bool TryGetExpressionCategory(
            ValueExpression expression,
            UnitSourceSchema schema,
            int depth,
            out UnitValueCategory category)
        {
            category = UnitValueCategory.None;
            if (expression == null || depth >= MaxExpressionDepth)
                return false;

            switch (expression.Kind)
            {
                case ValueExpressionKind.Literal:
                    category = expression.Literal.Category;
                    return category != UnitValueCategory.None;

                case ValueExpressionKind.Getter:
                    if (!schema.TryGet(expression.GetterKey, out UnitSourceGetSchemaEntry getter) ||
                        !HasCompatibleInputs(expression.Inputs, getter.Parameters, schema, depth + 1))
                    {
                        return false;
                    }

                    category = getter.ReturnType;
                    return category != UnitValueCategory.None;

                case ValueExpressionKind.Operation:
                    if (!s_comparatorFactory.TryCreateValueOperation(expression.OperationType, out IValueOperation operation) ||
                        !HasCompatibleInputs(expression.Inputs, operation.Parameters, schema, depth + 1))
                    {
                        return false;
                    }

                    category = operation.ResultCategory;
                    return category != UnitValueCategory.None;

                default:
                    return false;
            }
        }

        private static bool HasCompatibleInputs(
            IReadOnlyList<ValueExpression> expressions,
            IReadOnlyList<ComparatorParameterDefinition> parameters,
            UnitSourceSchema schema,
            int depth)
        {
            if (expressions == null || parameters == null || expressions.Count != parameters.Count)
                return false;

            for (int i = 0; i < parameters.Count; i++)
            {
                if (!TryGetExpressionCategory(expressions[i], schema, depth + 1, out UnitValueCategory category) ||
                    !parameters[i].Accepts(category))
                {
                    return false;
                }
            }

            return true;
        }

        private static ComparatorFactory CreateComparatorFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }
    }

    internal sealed class StateScriptRuntimeDataInspector
    {
        private const int MaxExpressionDepth = 16;

        private static readonly UnitSourceSchema s_sourceSchema = UnitSourceSchemaFactory.CreateForAllSources();

        private readonly Dictionary<string, HashSet<Type>> _nodeComponentTypes = new(StringComparer.Ordinal);
        private StateScriptGraphDebugSnapshot _runtime;
        private string _selectedNodeGuid;

        public void Refresh(StateScriptGraphDebugSnapshot runtime)
        {
            if (ReferenceEquals(_runtime, runtime))
                return;

            StateScriptInstanceData previousData = _runtime?.Data;
            _runtime = runtime;
            if (ReferenceEquals(previousData, runtime?.Data))
                return;

            _nodeComponentTypes.Clear();
            if (runtime == null)
                return;

            for (int i = 0; i < runtime.Data.Nodes.Count; i++)
                CollectNodeComponents(runtime.Data.Nodes[i]);
        }

        public void Invalidate()
        {
            _runtime = null;
            _nodeComponentTypes.Clear();
            _selectedNodeGuid = null;
        }

        public void SetSelectedNode(string nodeGuid)
        {
            _selectedNodeGuid = nodeGuid;
        }

        public void Draw(StateScriptGraphDebugSnapshot runtime)
        {
            Refresh(runtime);
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Component Data", EditorStyles.boldLabel);
            if (runtime == null)
            {
                EditorGUILayout.HelpBox("Select a live unit and graph to inspect runtime data.", MessageType.Info);
                return;
            }

            DrawComponentData(runtime);
        }

        private void DrawComponentData(StateScriptGraphDebugSnapshot runtime)
        {
            UnitRuntimeDrawerContext context = new(runtime.EntityManager, runtime.Entity, string.Empty, null);
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

        private void CollectNodeComponents(StateScriptNodeData node)
        {
            if (node == null)
                return;

            switch (node)
            {
                case CompareStateScriptNodeData compare:
                    CollectCondition(node.Guid, compare.Condition);
                    break;

                case MonitorStateScriptNodeData monitor:
                    CollectCondition(node.Guid, monitor.Condition);
                    break;

                case SetValueStateScriptNodeData setValue:
                    TrackSource(node.Guid, setValue.SetterKey);
                    CollectSetValueInputs(node.Guid, setValue);
                    break;

                case RequestSkillActionNodeData requestSkill:
                    TrackComponent(node.Guid, typeof(UnitSkillReleaseComponent));
                    CollectSkillRequestInputs(node.Guid, requestSkill.SkillId, requestSkill.Input);
                    break;

                case RequestSkillWithAdditionActionNodeData requestSkillWithAddition:
                    TrackComponent(node.Guid, typeof(UnitSkillReleaseComponent));
                    CollectSkillRequestInputs(node.Guid, requestSkillWithAddition.SkillId, requestSkillWithAddition.Input);
                    break;

                case RequestInteractionActionNodeData requestInteraction:
                    CollectExpression(node.Guid, requestInteraction.Target, 0);
                    break;

                case CompleteInteractionActionNodeData completeInteraction:
                    CollectExpression(node.Guid, completeInteraction.Result, 0);
                    break;

                case PublishGameEventStateScriptNodeData publishGameEvent:
                    CollectExpression(node.Guid, publishGameEvent.Reference, 0);
                    break;

                case QueryUnitsActionNodeData queryUnits:
                    TrackComponent(node.Guid, typeof(UnitVariableComponent));
                    CollectExpression(node.Guid, queryUnits.Center, 0);
                    CollectExpression(node.Guid, queryUnits.Direction, 0);
                    CollectExpression(node.Guid, queryUnits.Size, 0);
                    CollectExpression(node.Guid, queryUnits.Radius, 0);
                    CollectExpression(node.Guid, queryUnits.Angle, 0);
                    break;

                case ExecuteEffectActionNodeData executeEffect:
                    CollectExpression(node.Guid, executeEffect.TargetEntity, 0);
                    CollectExpression(node.Guid, executeEffect.OtherEntity, 0);
                    CollectExpression(node.Guid, executeEffect.Position, 0);
                    CollectExpression(node.Guid, executeEffect.TriggerValue, 0);
                    CollectExpression(node.Guid, executeEffect.SourceSkillId, 0);
                    break;

                case TimerStateScriptNodeData timer:
                    CollectExpression(node.Guid, timer.Duration, 0);
                    break;

                case NumberMonitorStateScriptNodeData numberMonitor:
                    CollectExpression(node.Guid, numberMonitor.Value, 0);
                    break;
            }
        }

        private void CollectCondition(string nodeGuid, ConditionConfig condition)
        {
            if (condition?.Inputs == null)
                return;

            for (int i = 0; i < condition.Inputs.Count; i++)
                CollectExpression(nodeGuid, condition.Inputs[i], 0);
        }

        private void CollectSetValueInputs(string nodeGuid, SetValueStateScriptNodeData setValue)
        {
            IReadOnlyList<ValueExpression> values = setValue.Values != null && setValue.Values.Count > 0
                ? setValue.Values
                : new[] { setValue.Value };
            for (int i = 0; i < values.Count; i++)
                CollectExpression(nodeGuid, values[i], 0);
        }

        private void CollectSkillRequestInputs(string nodeGuid, ValueExpression skillId, SkillRequestInputData input)
        {
            CollectExpression(nodeGuid, skillId, 0);
            if (input == null)
                return;

            CollectExpression(nodeGuid, input.Position, 0);
            CollectExpression(nodeGuid, input.TargetEntity, 0);
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
            if (string.IsNullOrWhiteSpace(sourceKey))
                return;

            if (s_sourceSchema.TryGet(sourceKey, out UnitSourceGetSchemaEntry getEntry))
            {
                TrackComponent(nodeGuid, getEntry.ComponentType);
                return;
            }

            if (s_sourceSchema.TryGet(sourceKey, out UnitSourceSetSchemaEntry setEntry))
            {
                TrackComponent(nodeGuid, setEntry.ComponentType);
                return;
            }

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

    internal static class StateScriptGraphValidator
    {
        public static List<string> Validate(StateScriptInstanceData graph)
        {
            List<string> errors = new();
            if (graph == null)
            {
                errors.Add("No graph selected.");
                return errors;
            }

            graph.EnsureValid();
            int entryCount = graph.Nodes.Count(node => node is StateScriptEntryNodeData);
            StateScriptNodeData entry = graph.Nodes.FirstOrDefault(node => node != null && node.Guid == graph.EntryNodeGuid);
            if (entryCount != 1 || entry is not StateScriptEntryNodeData)
                errors.Add("Graph needs one Entry node.");

            HashSet<string> nodeGuids = new(StringComparer.Ordinal);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                StateScriptNodeData node = graph.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.Guid) || !nodeGuids.Add(node.Guid))
                    errors.Add("Every node needs a unique Guid.");
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                StateScriptEdgeData edge = graph.Edges[i];
                if (edge == null || !nodeGuids.Contains(edge.OutputNodeGuid) || !nodeGuids.Contains(edge.InputNodeGuid))
                    errors.Add("Graph contains an edge with a missing node.");
            }

            return errors.Distinct().ToList();
        }
    }
}
