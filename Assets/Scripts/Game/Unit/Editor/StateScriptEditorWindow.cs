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
        private const float ListPanelWidth = 270f;
        private const float InspectorPanelWidth = 300f;

        private readonly List<StateScriptData> _rows = new();
        private readonly List<UnitPrefabEntry> _unitEntries = new();
        private int _selectedUnitDataId = -1;
        private string _selectedGraphGuid;
        private UnitSourceSchema _selectedSourceSchema;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private Vector2 _listScroll;

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
            if (!Application.isPlaying || _graphView == null || SelectedGraph == null)
                return;

            StateScriptRuntime runtime = FindDebugRuntime();
            _graphView.RefreshRuntimeDebug(runtime);
            Repaint();
        }

        private void BuildToolbar(VisualElement root)
        {
            Toolbar toolbar = new();
            toolbar.Add(CreateToolbarButton("Load", 48f, LoadData));
            toolbar.Add(CreateToolbarButton(_isDirty ? "Save *" : "Save", 58f, SaveData));
            toolbar.Add(CreateToolbarButton("Add Graph", 76f, AddGraph));
            toolbar.Add(CreateToolbarButton("Delete Graph", 92f, DeleteSelectedGraph));
            toolbar.Add(CreateToolbarButton("Validate", 64f, ValidateSelectedGraph));
            toolbar.Add(CreateToolbarButton("Generate Registry", 110f, StateScriptRegistryGenerator.Generate));
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

            _graphView = new StateScriptGraphView(this)
            {
                style = { flexGrow = 1f },
            };
            _graphView.RegisterCallback<MouseUpEvent>(_ => _inspectorContainer?.MarkDirtyRepaint());
            _graphView.RegisterCallback<KeyUpEvent>(_ => _inspectorContainer?.MarkDirtyRepaint());
            body.Add(_graphView);
            body.Add(CreateDivider());

            VisualElement inspectorPanel = new()
            {
                style =
                {
                    width = InspectorPanelWidth,
                    minWidth = InspectorPanelWidth,
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
            body.Add(inspectorPanel);
            root.Add(body);
        }

        private void DrawListPanel()
        {
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Units", EditorStyles.boldLabel);

            for (int i = 0; i < _unitEntries.Count; i++)
            {
                UnitPrefabEntry entry = _unitEntries[i];
                bool selected = entry.UnitData != null && entry.UnitData.Id == _selectedUnitDataId;
                GUIStyle style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (!GUILayout.Button(entry.DisplayName, style))
                    continue;

                if (entry.UnitData == null)
                {
                    SetStatus($"UnitData is missing for prefab: {entry.DisplayName}");
                    continue;
                }

                SelectUnit(entry.UnitData.Id);
            }

            EditorGUILayout.Space(10f);
            UnitPrefabEntry selectedEntry = GetSelectedUnitEntry();
            if (selectedEntry == null)
            {
                EditorGUILayout.HelpBox("Select a unit prefab.", MessageType.Info);
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
                    if (GUILayout.Button(string.IsNullOrWhiteSpace(graph.Name) ? "Unnamed Graph" : graph.Name,
                        selected ? EditorStyles.toolbarButton : EditorStyles.miniButton))
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

            EditorGUILayout.Space(8f);
            if (selectedEntry.Prefab.GetComponent<UnitStateScriptAuthoring>() == null)
                EditorGUILayout.HelpBox("Attach UnitStateScriptAuthoring to this prefab before using its graph at runtime.", MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        private void DrawInspectorPanel()
        {
            StateScriptNodeInspector.Draw(_graphView?.GetSelectedNodeData(), SelectedSourceSchema, MarkDirty);
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
            if (GetSelectedUnitEntry() == null)
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
            SaveGraphViewTransform();
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].EnsureValid();

            TableWrapper wrapper = new() { Rows = _rows };
            string json = JsonConvert.SerializeObject(wrapper, JsonSettings);
            DataFileUtility.WriteJsonText(DataPath, json);
            AssetDatabase.Refresh();
            _isDirty = false;
            SetStatus("Saved.");
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
                    Id = GetNextDataId(),
                    UnitDataId = entry.UnitData.Id,
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
            SetStatus("Modified.");
            _inspectorContainer?.MarkDirtyRepaint();
        }

        internal void RebuildGraph()
        {
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
            _selectedUnitDataId = unitDataId;
            _selectedSourceSchema = UnitSourceSchemaFactory.CreateForPrefab(GetSelectedUnitEntry()?.Prefab);
            StateScriptData data = GetSelectedData();
            _selectedGraphGuid = data?.Graphs.FirstOrDefault(graph => graph != null)?.Guid;
            RebuildGraph();
        }

        private StateScriptData GetSelectedData()
        {
            return _rows.FirstOrDefault(row => row.UnitDataId == _selectedUnitDataId);
        }

        private UnitPrefabEntry GetSelectedUnitEntry()
        {
            return _unitEntries.FirstOrDefault(entry => entry.UnitData != null && entry.UnitData.Id == _selectedUnitDataId);
        }

        private UnitSourceSchema SelectedSourceSchema => _selectedSourceSchema ?? s_emptySourceSchema;

        private void RefreshUnitEntries()
        {
            _unitEntries.Clear();
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
            }

            _unitEntries.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
        }

        private StateScriptRuntime FindDebugRuntime()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return null;

            EntityManager entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitStateScriptComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                UnitStateScriptComponent component = entityManager.GetComponentObject<UnitStateScriptComponent>(entities[i]);
                if (component == null || component.UnitDataId != _selectedUnitDataId)
                    continue;

                for (int runtimeIndex = 0; runtimeIndex < component.Runtimes.Count; runtimeIndex++)
                {
                    StateScriptRuntime runtime = component.Runtimes[runtimeIndex];
                    if (string.Equals(runtime.Data.Guid, _selectedGraphGuid, StringComparison.Ordinal))
                        return runtime;
                }
            }

            return null;
        }

        private int GetNextDataId()
        {
            int id = 1;
            HashSet<int> ids = new(_rows.Select(row => row.Id));
            while (ids.Contains(id))
                id++;
            return id;
        }

        private static ToolbarButton CreateToolbarButton(string text, float width, Action action)
        {
            return new ToolbarButton(action) { text = text, style = { width = width } };
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
