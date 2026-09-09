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

        private StateScriptGraphView _graphView;
        private IMGUIContainer _inspectorContainer;
        private Label _statusLabel;

        private static readonly UnitSourceSchema s_emptySourceSchema = new UnitSourceSchemaBuilder().Build();

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

            if (SelectedGraph != null)
                RebuildGraph();
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup >= _nextRuntimeUnitRefreshTime)
                RefreshRuntimeUnitEntries();

            if (_graphView == null || SelectedGraph == null)
            {
                Repaint();
                return;
            }

            StateScriptRuntime runtime = FindDebugRuntime();
            _runtimeDataInspector.Refresh(runtime);
            _graphView.RefreshRuntimeDebug(runtime);
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
            _selectedRuntimeEntity = Entity.Null;
            _nextRuntimeUnitRefreshTime = 0d;
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
            if (Application.isPlaying)
                DrawRuntimeUnitList();
            else
                DrawPrefabUnitList();

            if (Application.isPlaying && _selectedRuntimeEntity == Entity.Null)
            {
                EditorGUILayout.HelpBox("Select a live unit with a StateScript component to inspect its graphs.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(10f);
            UnitPrefabEntry selectedEntry = GetSelectedUnitEntry();
            if (selectedEntry == null)
            {
                string message = Application.isPlaying
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
                }
            }

            if (Application.isPlaying)
                _runtimeDataInspector.Draw(FindDebugRuntime(), SelectRuntimeDebugNode);

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

        private void SelectRuntimeDebugNode(string nodeGuid)
        {
            if (_graphView == null || !_graphView.SelectNode(nodeGuid))
                return;

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
            SetStatus("Modified.");
            _inspectorContainer?.MarkDirtyRepaint();
        }

        internal void RebuildGraph()
        {
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

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitPrefabDirectory });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
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
            if (!Application.isPlaying)
            {
                _selectedRuntimeEntity = Entity.Null;
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitStateScriptComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            bool selectedEntityFound = _selectedRuntimeEntity == Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                UnitStateScriptComponent component = entityManager.GetComponentObject<UnitStateScriptComponent>(entity);
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
        }

        private StateScriptRuntime FindDebugRuntime()
        {
            if (!Application.isPlaying || _selectedRuntimeEntity == Entity.Null)
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

            UnitStateScriptComponent component = entityManager.GetComponentObject<UnitStateScriptComponent>(_selectedRuntimeEntity);
            if (component == null)
                return null;

            for (int runtimeIndex = 0; runtimeIndex < component.Runtimes.Count; runtimeIndex++)
            {
                StateScriptRuntime runtime = component.Runtimes[runtimeIndex];
                if (runtime != null && string.Equals(runtime.Data?.Guid, _selectedGraphGuid, StringComparison.Ordinal))
                    return runtime;
            }

            return null;
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

        private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

        private readonly List<RuntimeInputBinding> _inputBindings = new();
        private readonly List<StateScriptRuntimeDebugValue> _nodeDebugValues = new();
        private StateScriptRuntime _runtime;
        private string _selectedNodeGuid;

        public void Refresh(StateScriptRuntime runtime)
        {
            if (ReferenceEquals(_runtime, runtime))
                return;

            _runtime = runtime;
            _inputBindings.Clear();
            if (runtime == null)
                return;

            for (int i = 0; i < runtime.NodesInTraversalOrder.Count; i++)
                CollectNodeInputs(runtime, runtime.NodesInTraversalOrder[i].Data);
        }

        public void Invalidate()
        {
            _runtime = null;
            _inputBindings.Clear();
            _selectedNodeGuid = null;
        }

        public void SetSelectedNode(string nodeGuid)
        {
            _selectedNodeGuid = nodeGuid;
        }

        public void Draw(StateScriptRuntime runtime, Action<string> onNodeSelected)
        {
            Refresh(runtime);
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Runtime Data", EditorStyles.boldLabel);
            if (runtime == null)
            {
                EditorGUILayout.HelpBox("Select a live unit and graph to inspect runtime data.", MessageType.Info);
                return;
            }

            DrawInputSection(onNodeSelected);
            DrawNodeStateSection(runtime, onNodeSelected);
        }

        private void DrawInputSection(Action<string> onNodeSelected)
        {
            EditorGUILayout.LabelField("Inputs", EditorStyles.miniBoldLabel);
            if (_inputBindings.Count == 0)
            {
                EditorGUILayout.HelpBox("This graph does not read any runtime input values.", MessageType.None);
                return;
            }

            for (int i = 0; i < _inputBindings.Count; i++)
            {
                RuntimeInputBinding binding = _inputBindings[i];
                EditorGUILayout.BeginVertical("box");
                DrawNodeHeader(binding.NodeGuid, binding.NodeLabel, onNodeSelected);
                EditorGUILayout.LabelField(binding.Path, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(binding.Key, EditorStyles.miniLabel);

                if (binding.TryRead(out UnitValue value, out string parameters, out string error))
                {
                    if (!string.IsNullOrEmpty(parameters))
                        EditorGUILayout.LabelField("Parameters", parameters, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Value", FormatUnitValue(value), EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox(error, MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawNodeStateSection(StateScriptRuntime runtime, Action<string> onNodeSelected)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Node State", EditorStyles.miniBoldLabel);
            for (int i = 0; i < runtime.NodesInTraversalOrder.Count; i++)
            {
                StateScriptNode node = runtime.NodesInTraversalOrder[i];
                _nodeDebugValues.Clear();
                node.CollectRuntimeDebugData(_nodeDebugValues);

                EditorGUILayout.BeginVertical("box");
                DrawNodeHeader(node.Data.Guid, GetNodeLabel(node.Data), onNodeSelected);
                for (int valueIndex = 0; valueIndex < _nodeDebugValues.Count; valueIndex++)
                {
                    StateScriptRuntimeDebugValue value = _nodeDebugValues[valueIndex];
                    EditorGUILayout.LabelField(value.Name, value.Value, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawNodeHeader(string nodeGuid, string nodeLabel, Action<string> onNodeSelected)
        {
            Color originalColor = GUI.backgroundColor;
            if (string.Equals(nodeGuid, _selectedNodeGuid, StringComparison.Ordinal))
                GUI.backgroundColor = new Color(0.38f, 0.70f, 1f, 1f);

            if (GUILayout.Button(nodeLabel, EditorStyles.miniButton))
                onNodeSelected?.Invoke(nodeGuid);

            GUI.backgroundColor = originalColor;
        }

        private void CollectNodeInputs(StateScriptRuntime runtime, StateScriptNodeData node)
        {
            if (node == null)
                return;

            DebugNodeReference nodeReference = new(node.Guid, GetNodeLabel(node));
            switch (node)
            {
                case CompareStateScriptNodeData compare:
                    CollectCondition(runtime, nodeReference, "Condition", compare.Condition);
                    break;

                case MonitorStateScriptNodeData monitor:
                    CollectCondition(runtime, nodeReference, "Condition", monitor.Condition);
                    break;

                case SetValueStateScriptNodeData setValue:
                    CollectSetValueInputs(runtime, nodeReference, setValue);
                    break;

                case RequestSkillActionNodeData requestSkill:
                    CollectExpression(runtime, nodeReference, "Skill ID", requestSkill.SkillId, 0);
                    break;

                case RequestInteractionActionNodeData requestInteraction:
                    CollectInteractionInput(runtime, nodeReference, requestInteraction.Interaction);
                    break;

                case PublishGameEventStateScriptNodeData publishGameEvent:
                    CollectExpression(runtime, nodeReference, "Reference", publishGameEvent.Reference, 0);
                    break;

                case TimerStateScriptNodeData timer:
                    CollectExpression(runtime, nodeReference, "Duration", timer.Duration, 0);
                    break;

                case NumberMonitorStateScriptNodeData numberMonitor:
                    CollectExpression(runtime, nodeReference, "Observed Value", numberMonitor.Value, 0);
                    break;
            }
        }

        private void CollectCondition(
            StateScriptRuntime runtime,
            DebugNodeReference node,
            string path,
            ConditionConfig condition)
        {
            if (condition?.Inputs == null)
                return;

            for (int i = 0; i < condition.Inputs.Count; i++)
                CollectExpression(runtime, node, $"{path}[{i}]", condition.Inputs[i], 0);
        }

        private void CollectSetValueInputs(
            StateScriptRuntime runtime,
            DebugNodeReference node,
            SetValueStateScriptNodeData setValue)
        {
            IReadOnlyList<ValueExpression> values = setValue.Values != null && setValue.Values.Count > 0
                ? setValue.Values
                : new[] { setValue.Value };
            for (int i = 0; i < values.Count; i++)
                CollectExpression(runtime, node, $"Value[{i}]", values[i], 0);
        }

        private void CollectInteractionInput(
            StateScriptRuntime runtime,
            DebugNodeReference node,
            InteractionRequestInput interaction)
        {
            if (interaction == null)
                return;

            if (interaction.Source == InteractionRequestSource.Getter)
            {
                _inputBindings.Add(RuntimeInputBinding.CreateInteraction(
                    node,
                    "Interaction",
                    interaction.GetterKey,
                    runtime.Sources));
                return;
            }

            CollectExpression(runtime, node, "Interaction Target", interaction.Target, 0);
        }

        private void CollectExpression(
            StateScriptRuntime runtime,
            DebugNodeReference node,
            string path,
            ValueExpression expression,
            int depth)
        {
            if (expression == null || depth >= MaxExpressionDepth)
                return;

            if (expression.Kind == ValueExpressionKind.Getter)
            {
                _inputBindings.Add(RuntimeInputBinding.CreateValue(
                    node,
                    path,
                    expression,
                    runtime.Sources));
            }

            if (expression.Inputs == null)
                return;

            for (int i = 0; i < expression.Inputs.Count; i++)
                CollectExpression(runtime, node, $"{path}.Input[{i}]", expression.Inputs[i], depth + 1);
        }

        private static string GetNodeLabel(StateScriptNodeData node)
        {
            string displayName = StateScriptNodeDataRegistry.GetDisplayName(node.Type);
            return string.IsNullOrWhiteSpace(node.Guid)
                ? displayName
                : $"{displayName} ({node.Guid})";
        }

        private static string FormatUnitValue(UnitValue value)
        {
            return value.Type switch
            {
                UnitValueType.Bool => value.Bool.ToString(),
                UnitValueType.Int => value.Int.ToString(),
                UnitValueType.Float => value.Float.ToString("0.###"),
                UnitValueType.Float2 => $"({value.Float2.x:0.###}, {value.Float2.y:0.###})",
                UnitValueType.Float3 => $"({value.Float3.x:0.###}, {value.Float3.y:0.###}, {value.Float3.z:0.###})",
                UnitValueType.Entity => value.Entity.ToString(),
                UnitValueType.String => value.String ?? string.Empty,
                _ => "(none)",
            };
        }

        private static ComparatorFactory CreateExpressionFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }

        private readonly struct DebugNodeReference
        {
            public DebugNodeReference(string guid, string label)
            {
                Guid = guid ?? string.Empty;
                Label = label ?? string.Empty;
            }

            public string Guid { get; }
            public string Label { get; }
        }

        private sealed class RuntimeInputBinding
        {
            private readonly Func<UnitValue> _valueReader;
            private readonly Func<UnitValue>[] _parameterReaders;
            private readonly UnitSourceAccessTable _sources;
            private readonly bool _isInteraction;
            private readonly string _buildError;

            private RuntimeInputBinding(
                DebugNodeReference node,
                string path,
                string key,
                Func<UnitValue> valueReader,
                Func<UnitValue>[] parameterReaders,
                UnitSourceAccessTable sources,
                bool isInteraction,
                string buildError)
            {
                NodeGuid = node.Guid;
                NodeLabel = node.Label;
                Path = path;
                Key = key;
                _valueReader = valueReader;
                _parameterReaders = parameterReaders;
                _sources = sources;
                _isInteraction = isInteraction;
                _buildError = buildError;
            }

            public string NodeGuid { get; }
            public string NodeLabel { get; }
            public string Path { get; }
            public string Key { get; }

            public static RuntimeInputBinding CreateValue(
                DebugNodeReference node,
                string path,
                ValueExpression expression,
                UnitSourceAccessTable sources)
            {
                if (!s_expressionFactory.TryBuildValueExpression(
                        expression,
                        sources,
                        out _,
                        out Func<UnitValue> valueReader,
                        out string error))
                {
                    return new RuntimeInputBinding(node, path, expression.GetterKey, null, null, sources, false, error);
                }

                Func<UnitValue>[] parameterReaders = new Func<UnitValue>[expression.Inputs?.Count ?? 0];
                for (int i = 0; i < parameterReaders.Length; i++)
                {
                    if (!s_expressionFactory.TryBuildValueExpression(
                            expression.Inputs[i],
                            sources,
                            out _,
                            out Func<UnitValue> parameterReader,
                            out error))
                    {
                        return new RuntimeInputBinding(node, path, expression.GetterKey, null, null, sources, false, error);
                    }

                    parameterReaders[i] = parameterReader;
                }

                return new RuntimeInputBinding(node, path, expression.GetterKey, valueReader, parameterReaders, sources, false, string.Empty);
            }

            public static RuntimeInputBinding CreateInteraction(
                DebugNodeReference node,
                string path,
                string key,
                UnitSourceAccessTable sources)
            {
                string error = sources.TryGetInteractionDefinition(key, out _)
                    ? string.Empty
                    : $"Interaction getter '{key}' is unavailable.";
                return new RuntimeInputBinding(node, path, key, null, null, sources, true, error);
            }

            public bool TryRead(out UnitValue value, out string parameters, out string error)
            {
                value = UnitValue.None;
                parameters = string.Empty;
                error = _buildError;
                if (!string.IsNullOrEmpty(error))
                    return false;

                try
                {
                    if (_isInteraction)
                    {
                        if (!_sources.TryGetInteraction(Key, out InteractionRequestSnapshot request))
                        {
                            value = UnitValue.FromString("(no active interaction request)");
                            return true;
                        }

                        value = UnitValue.FromString(FormatInteractionRequest(request));
                        return true;
                    }

                    if (_parameterReaders.Length > 0)
                    {
                        string[] formattedParameters = new string[_parameterReaders.Length];
                        for (int i = 0; i < _parameterReaders.Length; i++)
                            formattedParameters[i] = FormatUnitValue(_parameterReaders[i]());
                        parameters = string.Join(", ", formattedParameters);
                    }

                    value = _valueReader();
                    if (value.Category == UnitValueCategory.None)
                    {
                        error = "Getter returned no value.";
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            private static string FormatInteractionRequest(InteractionRequestSnapshot request)
            {
                return $"Target={request.Target}; Kind={request.Data.Kind}; DataId={request.Data.DataId}; " +
                       $"Amount={request.Data.Amount}; Variant={request.Data.Variant}";
            }
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
