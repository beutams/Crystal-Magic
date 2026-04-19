using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Editor.Data
{
    public class NPCEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/NPCDataTable.json";
        private const float ListPanelWidth = 220f;
        private const float InsertFieldWidth = 30f;
        private const float LabelWidth = 150f;
        private const float GraphNodeWidth = 460f;
        private const float GraphNodeGap = 28f;
        private const float GraphLevelGap = 44f;
        private const float GraphTerminalWidth = 240f;
        private const float GraphTerminalHeight = 28f;

        private List<NPCData> _rows = new();
        private bool _isDirty;
        private string _statusText = string.Empty;

        private int _selectedIndex = -1;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private readonly Dictionary<NPCData, string> _insertTexts = new();

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private class TableWrapper
        {
            public List<NPCData> Rows = new();
        }

        private readonly struct GraphEdge
        {
            public GraphEdge(string fromGuid, string toGuid, string label)
            {
                FromGuid = fromGuid;
                ToGuid = toGuid;
                Label = label;
            }

            public string FromGuid { get; }
            public string ToGuid { get; }
            public string Label { get; }
        }

        [MenuItem("Tools/Data/NPC Editor")]
        public static void Open()
        {
            NPCEditorWindow window = GetWindow<NPCEditorWindow>("NPC Editor");
            window.minSize = new Vector2(920f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadData();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDivider();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                LoadData();
            }

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                SaveData();
            }
            GUI.enabled = true;

            if (GUILayout.Button("+ Add", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                AddNpc();
            }

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                DeleteSelected();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
            {
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth));
            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            Event evt = Event.current;
            NPCData moveRow = null;
            int moveToIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                NPCData row = _rows[i];
                EditorGUILayout.BeginHorizontal();
                string insertText = _insertTexts.TryGetValue(row, out string currentInsertText) ? currentInsertText : string.Empty;
                string controlName = $"insert_{row.GetHashCode()}";
                GUI.SetNextControlName(controlName);
                string newInsertText = EditorGUILayout.TextField(insertText, GUILayout.Width(InsertFieldWidth));
                if (newInsertText != insertText)
                {
                    if (string.IsNullOrWhiteSpace(newInsertText))
                    {
                        _insertTexts.Remove(row);
                    }
                    else
                    {
                        _insertTexts[row] = newInsertText;
                    }
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

                string label = $"{row.Id}  {GetListName(row)}";
                bool selected = i == _selectedIndex;
                if (GUILayout.Toggle(selected, label, "Button"))
                {
                    _selectedIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            if (moveRow != null)
            {
                MoveRowToInsertIndex(_rows.IndexOf(moveRow), moveToIndex);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                EditorGUILayout.HelpBox("Select one NPC row.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            NPCData row = _rows[_selectedIndex];
            if (EnsureNpcValid(row))
            {
                _isDirty = true;
            }

            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUILayout.LabelField("Basic", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", row.Id);
            row.NPC = EditorGUILayout.TextField("NPC", row.NPC ?? string.Empty);
            row.DisplayName = EditorGUILayout.TextField("Display Name", row.DisplayName ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Interactions", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Interaction", GUILayout.Width(110f)))
            {
                AddInteraction(row);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < row.Interactions.Count; i++)
            {
                DrawInteraction(row, i);
            }

            if (row.Interactions.Count == 0)
            {
                EditorGUILayout.HelpBox("No interaction configured.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawInteraction(NPCData row, int index)
        {
            NPCInteractionData interaction = row.Interactions[index];
            if (interaction == null)
            {
                interaction = new NPCInteractionData();
                row.Interactions[index] = interaction;
                _isDirty = true;
            }

            if (EnsureInteractionValid(interaction))
            {
                _isDirty = true;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Interaction {index + 1}", EditorStyles.boldLabel);

            GUI.enabled = index > 0;
            if (GUILayout.Button("Up", GUILayout.Width(52f)))
            {
                SwapInteractions(row, index, index - 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.enabled = index < row.Interactions.Count - 1;
            if (GUILayout.Button("Down", GUILayout.Width(52f)))
            {
                SwapInteractions(row, index, index + 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.enabled = true;
            if (GUILayout.Button("Delete", GUILayout.Width(52f)))
            {
                row.Interactions.RemoveAt(index);
                _isDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            interaction.Key = EditorGUILayout.TextField("Key", interaction.Key ?? string.Empty);
            interaction.DisplayName = EditorGUILayout.TextField("Display Name", interaction.DisplayName ?? string.Empty);
            interaction.EnableExpression = EditorGUILayout.TextField("Enable Expression", interaction.EnableExpression ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            EditorGUILayout.Space(6f);
            DrawInteractionGraph(interaction);

            EditorGUILayout.EndVertical();
        }

        private void DrawInteractionGraph(NPCInteractionData interaction)
        {
            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            interaction.EntryNodeGuid = DrawNodeGuidPopup("Entry Node", interaction, interaction.EntryNodeGuid, null);
            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            EditorGUILayout.Space(4f);
            DrawGraphTerminal("Start", new Color(0.23f, 0.49f, 0.76f));
            Rect startRect = GUILayoutUtility.GetLastRect();

            if (interaction.Nodes.Count == 0)
            {
                if (GUILayout.Button("Add Entry Node", GUILayout.Width(120f)))
                {
                    ShowAddEntryNodeMenu(interaction);
                }
                EditorGUILayout.HelpBox("No node configured.", MessageType.None);
                return;
            }

            List<List<NPCInteractionNodeData>> levels = BuildNodeLevels(interaction);
            List<GraphEdge> edges = CollectGraphEdges(interaction);
            Dictionary<string, Rect> nodeRects = new Dictionary<string, Rect>(StringComparer.Ordinal);
            for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                List<NPCInteractionNodeData> levelNodes = levels[levelIndex];
                for (int i = 0; i < levelNodes.Count; i++)
                {
                    Rect nodeRect = DrawGraphNode(interaction, levelNodes[i]);
                    nodeRects[levelNodes[i].Guid] = nodeRect;
                    if (i < levelNodes.Count - 1)
                    {
                        GUILayout.Space(GraphNodeGap);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (levelIndex < levels.Count - 1)
                {
                    GUILayout.Space(GraphLevelGap);
                }
            }

            DrawGraphTerminal("End", new Color(0.27f, 0.63f, 0.36f));
            Rect endRect = GUILayoutUtility.GetLastRect();
            DrawGraphLines(interaction, nodeRects, edges, startRect, endRect);
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"Missing file: {DataPath}. It will be created on save.";
                return;
            }

            try
            {
                string json = File.ReadAllText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                {
                    _rows = wrapper.Rows;
                }

                for (int i = 0; i < _rows.Count; i++)
                {
                    if (EnsureNpcValid(_rows[i]))
                    {
                        _isDirty = true;
                    }
                }

                NormalizeRowIds();
                _insertTexts.Clear();
                _statusText = $"Loaded {_rows.Count} row(s) | {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                Debug.LogError($"[NPCEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string dir = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                NormalizeRowIds();
                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                File.WriteAllText(DataPath, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} row(s) | {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                Debug.LogError($"[NPCEditor] Save error:\n{ex}");
            }
        }

        private void AddNpc()
        {
            int id = _rows.Count + 1;
            _rows.Add(new NPCData
            {
                Id = id,
                NPC = $"NPC_{id}",
                DisplayName = $"NPC {id}",
                Interactions = new List<NPCInteractionData>(),
            });
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                return;
            }

            NPCData row = _rows[_selectedIndex];
            bool confirmed = EditorUtility.DisplayDialog("Delete NPC", $"Delete '{GetListName(row)}'?", "Delete", "Cancel");
            if (!confirmed)
            {
                return;
            }

            _rows.RemoveAt(_selectedIndex);
            _insertTexts.Remove(row);
            NormalizeRowIds();
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Id = i + 1;
            }
        }

        private void MoveRowToInsertIndex(int fromIndex, int insertIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rows.Count)
            {
                return;
            }

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count - 1);
            if (fromIndex == insertIndex)
            {
                return;
            }

            NPCData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);

            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            _isDirty = true;
            GUI.FocusControl(null);
            Repaint();
        }

        private void AddInteraction(NPCData row)
        {
            if (EnsureNpcValid(row))
            {
                _isDirty = true;
            }
            row.Interactions.Add(new NPCInteractionData
            {
                Key = $"Interaction_{row.Interactions.Count + 1}",
                DisplayName = $"Interaction {row.Interactions.Count + 1}",
                EnableExpression = string.Empty,
                Nodes = new List<NPCInteractionNodeData>(),
            });
            _isDirty = true;
        }

        private void SwapInteractions(NPCData row, int fromIndex, int toIndex)
        {
            NPCInteractionData temp = row.Interactions[fromIndex];
            row.Interactions[fromIndex] = row.Interactions[toIndex];
            row.Interactions[toIndex] = temp;
            _isDirty = true;
        }

        private Rect DrawGraphNode(NPCInteractionData interaction, NPCInteractionNodeData node)
        {
            if (EnsureNodeValid(node))
            {
                _isDirty = true;
            }

            EditorGUILayout.BeginVertical("box", GUILayout.Width(GraphNodeWidth));

            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, GetGraphNodeColor(node));
            GUI.Label(new Rect(headerRect.x + 8f, headerRect.y + 3f, headerRect.width - 16f, headerRect.height - 6f),
                $"{NPCInteractionNodeDataRegistry.GetDisplayName(node.Type)}{(string.Equals(interaction.EntryNodeGuid, node.Guid, StringComparison.Ordinal) ? "  [ENTRY]" : string.Empty)}",
                EditorStyles.whiteBoldLabel);

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(42f));
            if (GUILayout.Button(NPCInteractionNodeDataRegistry.GetDisplayName(node.Type), EditorStyles.popup, GUILayout.Width(120f)))
            {
                ShowChangeNodeTypeMenu(interaction, node.Guid);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(56f)))
            {
                DeleteNode(interaction, node.Guid);
                _isDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return GUILayoutUtility.GetLastRect();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);

            EditorGUI.BeginChangeCheck();
            switch (node)
            {
                case NPCDialogueInteractionNodeData dialogue:
                    dialogue.Speaker = EditorGUILayout.TextField("Speaker", dialogue.Speaker ?? string.Empty);
                    dialogue.ContentKey = EditorGUILayout.TextField("Content Key", dialogue.ContentKey ?? string.Empty);
                    DrawBranchList(interaction, node);
                    break;
                case NPCSelectInteractionNodeData select:
                    DrawSelectNode(interaction, node, select);
                    break;
                case NPCOpenUIInteractionNodeData openUI:
                    openUI.UIName = EditorGUILayout.TextField("UI Name", openUI.UIName ?? string.Empty);
                    openUI.OpenData = EditorGUILayout.TextField("Open Data", openUI.OpenData ?? string.Empty);
                    openUI.WaitUntilClosed = EditorGUILayout.Toggle("Wait Until Closed", openUI.WaitUntilClosed);
                    DrawBranchList(interaction, node);
                    break;
                case NPCMoveInteractionNodeData move:
                    move.TargetMarker = EditorGUILayout.TextField("Target Marker", move.TargetMarker ?? string.Empty);
                    move.StopDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Stop Distance", move.StopDistance));
                    move.WaitUntilArrived = EditorGUILayout.Toggle("Wait Until Arrived", move.WaitUntilArrived);
                    DrawBranchList(interaction, node);
                    break;
                case NPCEnterDungeonInteractionNodeData enterDungeon:
                    enterDungeon.DungeonFloor = Mathf.Max(1, EditorGUILayout.IntField("Dungeon Floor", enterDungeon.DungeonFloor));
                    EditorGUILayout.HelpBox("This node immediately ends the current interaction and enters the dungeon flow.", MessageType.None);
                    break;
                case NPCEnterTrainingGroundInteractionNodeData:
                    EditorGUILayout.HelpBox("This node immediately ends the current interaction and enters the training ground flow.", MessageType.None);
                    break;
                default:
                    EditorGUILayout.HelpBox($"Unknown node type: {node.Type}", MessageType.Warning);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            EditorGUILayout.EndVertical();
            return GUILayoutUtility.GetLastRect();
        }

        private void DrawGraphTerminal(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Rect rect = GUILayoutUtility.GetRect(GraphTerminalWidth, GraphTerminalHeight, GUILayout.Width(GraphTerminalWidth), GUILayout.Height(GraphTerminalHeight));
            EditorGUI.DrawRect(rect, new Color(0.17f, 0.17f, 0.17f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), color);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 16f, rect.height - 8f), label, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphLines(NPCInteractionData interaction, Dictionary<string, Rect> nodeRects, List<GraphEdge> edges, Rect startRect, Rect endRect)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.60f, 0.68f, 0.78f);

            if (!string.IsNullOrWhiteSpace(interaction.EntryNodeGuid) && nodeRects.TryGetValue(interaction.EntryNodeGuid, out Rect entryRect))
            {
                DrawGraphLine(startRect.center, new Vector2(entryRect.center.x, entryRect.yMin));
            }

            HashSet<string> nodesWithOutgoing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                GraphEdge edge = edges[i];
                if (!nodeRects.TryGetValue(edge.FromGuid, out Rect fromRect) || !nodeRects.TryGetValue(edge.ToGuid, out Rect toRect))
                {
                    continue;
                }

                nodesWithOutgoing.Add(edge.FromGuid);
                Vector2 from = new Vector2(fromRect.center.x, fromRect.yMax);
                Vector2 to = new Vector2(toRect.center.x, toRect.yMin);
                DrawGraphLine(from, to);

                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    Vector2 mid = Vector2.Lerp(from, to, 0.5f);
                    GUI.Label(new Rect(mid.x - 90f, mid.y - 10f, 180f, 20f), edge.Label, EditorStyles.miniLabel);
                }
            }

            foreach ((string guid, Rect rect) in nodeRects)
            {
                if (nodesWithOutgoing.Contains(guid))
                {
                    continue;
                }

                DrawGraphLine(new Vector2(rect.center.x, rect.yMax), endRect.center);
            }

            Handles.EndGUI();
        }

        private static void DrawGraphLine(Vector2 from, Vector2 to)
        {
            Vector3 startTangent = from + Vector2.up * 28f;
            Vector3 endTangent = to + Vector2.down * 28f;
            Handles.DrawBezier(from, to, startTangent, endTangent, Handles.color, null, 2f);
        }

        private List<GraphEdge> CollectGraphEdges(NPCInteractionData interaction)
        {
            List<GraphEdge> edges = new List<GraphEdge>();
            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                NPCInteractionNodeData node = interaction.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (node is NPCSelectInteractionNodeData select)
                {
                    for (int optionIndex = 0; optionIndex < select.Options.Count; optionIndex++)
                    {
                        NPCSelectOptionData option = select.Options[optionIndex];
                        if (option == null || string.IsNullOrWhiteSpace(option.NextNodeGuid))
                        {
                            continue;
                        }

                        edges.Add(new GraphEdge(node.Guid, option.NextNodeGuid, option.DisplayName));
                    }
                    continue;
                }

                for (int branchIndex = 0; branchIndex < node.Branches.Count; branchIndex++)
                {
                    NPCInteractionBranchData branch = node.Branches[branchIndex];
                    if (branch == null || string.IsNullOrWhiteSpace(branch.NextNodeGuid))
                    {
                        continue;
                    }

                    string label = string.IsNullOrWhiteSpace(branch.CheckExpression) ? null : branch.CheckExpression;
                    edges.Add(new GraphEdge(node.Guid, branch.NextNodeGuid, label));
                }
            }

            return edges;
        }

        private void DrawBranchList(NPCInteractionData interaction, NPCInteractionNodeData node)
        {
            node.Branches ??= new List<NPCInteractionBranchData>();

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Successors", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Branch", GUILayout.Width(84f)))
            {
                node.Branches.Add(new NPCInteractionBranchData());
                _isDirty = true;
            }
            if (GUILayout.Button("Add Successor", GUILayout.Width(96f)))
            {
                ShowAddSuccessorNodeMenu(interaction, node, false);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < node.Branches.Count; i++)
            {
                NPCInteractionBranchData branch = node.Branches[i] ?? new NPCInteractionBranchData();
                node.Branches[i] = branch;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Branch {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    node.Branches.RemoveAt(i);
                    _isDirty = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                branch.CheckExpression = EditorGUILayout.TextField("Check", branch.CheckExpression ?? string.Empty);
                branch.NextNodeGuid = DrawNodeGuidPopup("Next Node", interaction, branch.NextNodeGuid, node.Guid);
                EditorGUILayout.EndVertical();
            }

            if (node.Branches.Count == 0)
            {
                EditorGUILayout.HelpBox("No successor configured.", MessageType.None);
            }
        }

        private void DrawSelectNode(NPCInteractionData interaction, NPCInteractionNodeData parentNode, NPCSelectInteractionNodeData select)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Option", GUILayout.Width(92f)))
            {
                select.Options.Add(new NPCSelectOptionData
                {
                    DisplayName = $"Option {select.Options.Count + 1}",
                });
                _isDirty = true;
            }
            if (GUILayout.Button("Add Option Node", GUILayout.Width(110f)))
            {
                ShowAddSuccessorNodeMenu(interaction, parentNode, true);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < select.Options.Count; i++)
            {
                NPCSelectOptionData option = select.Options[i] ?? new NPCSelectOptionData();
                select.Options[i] = option;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Option {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    select.Options.RemoveAt(i);
                    _isDirty = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                option.DisplayName = EditorGUILayout.TextField("Display Name", option.DisplayName ?? string.Empty);
                option.EnableExpression = EditorGUILayout.TextField("Enable Expression", option.EnableExpression ?? string.Empty);
                option.NextNodeGuid = DrawNodeGuidPopup("Next Node", interaction, option.NextNodeGuid, parentNode.Guid);
                EditorGUILayout.EndVertical();
            }

            if (select.Options.Count == 0)
            {
                EditorGUILayout.HelpBox("No option configured.", MessageType.None);
            }
        }

        private int FindNodeReferenceIndex(List<string> guids, string currentGuid)
        {
            for (int i = 0; i < guids.Count; i++)
            {
                if (string.Equals(guids[i], currentGuid, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private string DrawNodeGuidPopup(string label, NPCInteractionData interaction, string currentGuid, string excludeGuid)
        {
            List<string> guidOptions = new List<string> { string.Empty };
            List<string> displayOptions = new List<string> { "(None)" };

            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                NPCInteractionNodeData node = interaction.Nodes[i];
                if (node == null || string.Equals(node.Guid, excludeGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                guidOptions.Add(node.Guid);
                displayOptions.Add($"{i + 1}. {NPCInteractionNodeDataRegistry.GetDisplayName(node.Type)}");
            }

            int selectedIndex = FindNodeReferenceIndex(guidOptions, currentGuid);
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, displayOptions.ToArray());
            return guidOptions[newIndex];
        }

        private List<List<NPCInteractionNodeData>> BuildNodeLevels(NPCInteractionData interaction)
        {
            List<List<NPCInteractionNodeData>> levels = new List<List<NPCInteractionNodeData>>();
            Dictionary<string, int> depthMap = new Dictionary<string, int>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();

            if (!string.IsNullOrWhiteSpace(interaction.EntryNodeGuid) && interaction.GetNode(interaction.EntryNodeGuid) != null)
            {
                depthMap[interaction.EntryNodeGuid] = 0;
                queue.Enqueue(interaction.EntryNodeGuid);
            }

            while (queue.Count > 0)
            {
                string currentGuid = queue.Dequeue();
                NPCInteractionNodeData node = interaction.GetNode(currentGuid);
                if (node == null)
                {
                    continue;
                }

                int nextDepth = depthMap[currentGuid] + 1;
                List<string> successors = GetSuccessorGuids(node);
                for (int i = 0; i < successors.Count; i++)
                {
                    string successorGuid = successors[i];
                    if (string.IsNullOrWhiteSpace(successorGuid) || interaction.GetNode(successorGuid) == null || depthMap.ContainsKey(successorGuid))
                    {
                        continue;
                    }

                    depthMap[successorGuid] = nextDepth;
                    queue.Enqueue(successorGuid);
                }
            }

            int maxDepth = 0;
            foreach ((string _, int depth) in depthMap)
            {
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                }
            }

            for (int i = 0; i <= maxDepth; i++)
            {
                levels.Add(new List<NPCInteractionNodeData>());
            }

            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                NPCInteractionNodeData node = interaction.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (!depthMap.TryGetValue(node.Guid, out int depth))
                {
                    depth = levels.Count;
                    levels.Add(new List<NPCInteractionNodeData>());
                }

                levels[depth].Add(node);
            }

            return levels;
        }

        private List<string> GetSuccessorGuids(NPCInteractionNodeData node)
        {
            List<string> guids = new List<string>();
            if (node == null)
            {
                return guids;
            }

            if (node is NPCSelectInteractionNodeData select)
            {
                for (int i = 0; i < select.Options.Count; i++)
                {
                    NPCSelectOptionData option = select.Options[i];
                    if (option != null && !string.IsNullOrWhiteSpace(option.NextNodeGuid))
                    {
                        guids.Add(option.NextNodeGuid);
                    }
                }
            }
            else
            {
                for (int i = 0; i < node.Branches.Count; i++)
                {
                    NPCInteractionBranchData branch = node.Branches[i];
                    if (branch != null && !string.IsNullOrWhiteSpace(branch.NextNodeGuid))
                    {
                        guids.Add(branch.NextNodeGuid);
                    }
                }
            }

            return guids;
        }

        private void ShowAddEntryNodeMenu(NPCInteractionData interaction)
        {
            GenericMenu menu = new GenericMenu();
            foreach (string typeName in NPCInteractionNodeDataRegistry.TypeOrder)
            {
                string capturedTypeName = typeName;
                menu.AddItem(new GUIContent(NPCInteractionNodeDataRegistry.GetDisplayName(typeName)), false, () => AddEntryNode(interaction, capturedTypeName));
            }
            menu.ShowAsContext();
        }

        private void AddEntryNode(NPCInteractionData interaction, string typeName)
        {
            NPCInteractionNodeData node = NPCInteractionNodeDataRegistry.Create(typeName);
            if (node == null)
            {
                return;
            }

            interaction.Nodes.Add(node);
            interaction.EntryNodeGuid = node.Guid;
            _isDirty = true;
            Repaint();
        }

        private void ShowAddSuccessorNodeMenu(NPCInteractionData interaction, NPCInteractionNodeData parentNode, bool asSelectOption)
        {
            GenericMenu menu = new GenericMenu();
            foreach (string typeName in NPCInteractionNodeDataRegistry.TypeOrder)
            {
                string capturedTypeName = typeName;
                menu.AddItem(new GUIContent(NPCInteractionNodeDataRegistry.GetDisplayName(typeName)), false, () => AddSuccessorNode(interaction, parentNode, capturedTypeName, asSelectOption));
            }
            menu.ShowAsContext();
        }

        private void AddSuccessorNode(NPCInteractionData interaction, NPCInteractionNodeData parentNode, string typeName, bool asSelectOption)
        {
            NPCInteractionNodeData node = NPCInteractionNodeDataRegistry.Create(typeName);
            if (node == null)
            {
                return;
            }

            interaction.Nodes.Add(node);
            if (asSelectOption && parentNode is NPCSelectInteractionNodeData select)
            {
                select.Options.Add(new NPCSelectOptionData
                {
                    DisplayName = $"Option {select.Options.Count + 1}",
                    NextNodeGuid = node.Guid,
                });
            }
            else
            {
                parentNode.Branches ??= new List<NPCInteractionBranchData>();
                parentNode.Branches.Add(new NPCInteractionBranchData
                {
                    NextNodeGuid = node.Guid,
                });
            }

            if (string.IsNullOrWhiteSpace(interaction.EntryNodeGuid))
            {
                interaction.EntryNodeGuid = node.Guid;
            }

            _isDirty = true;
            Repaint();
        }

        private void ShowChangeNodeTypeMenu(NPCInteractionData interaction, string nodeGuid)
        {
            int index = interaction.GetNodeIndex(nodeGuid);
            if (index < 0)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            foreach (string typeName in NPCInteractionNodeDataRegistry.TypeOrder)
            {
                string capturedTypeName = typeName;
                bool isCurrent = string.Equals(
                    NPCInteractionNodeDataRegistry.ResolveTypeName(interaction.Nodes[index]),
                    capturedTypeName,
                    StringComparison.Ordinal);

                menu.AddItem(
                    new GUIContent(NPCInteractionNodeDataRegistry.GetDisplayName(typeName)),
                    isCurrent,
                    () => ChangeNodeType(interaction, index, capturedTypeName));
            }

            menu.ShowAsContext();
        }

        private void ChangeNodeType(NPCInteractionData interaction, int index, string typeName)
        {
            NPCInteractionNodeData oldNode = interaction.Nodes[index];
            string currentType = NPCInteractionNodeDataRegistry.ResolveTypeName(oldNode);
            if (string.Equals(currentType, typeName, StringComparison.Ordinal))
            {
                return;
            }

            NPCInteractionNodeData newNode = NPCInteractionNodeDataRegistry.Create(typeName);
            if (newNode == null)
            {
                return;
            }

            newNode.Guid = oldNode.Guid;
            newNode.Branches = oldNode.Branches ?? new List<NPCInteractionBranchData>();
            interaction.Nodes[index] = newNode;
            _isDirty = true;
            Repaint();
        }

        private void DeleteNode(NPCInteractionData interaction, string nodeGuid)
        {
            int index = interaction.GetNodeIndex(nodeGuid);
            if (index < 0)
            {
                return;
            }

            interaction.Nodes.RemoveAt(index);
            _isDirty = true;
        }

        private bool EnsureNpcValid(NPCData row)
        {
            bool changed = false;
            row.Interactions ??= new List<NPCInteractionData>();

            for (int i = 0; i < row.Interactions.Count; i++)
            {
                if (row.Interactions[i] == null)
                {
                    row.Interactions[i] = new NPCInteractionData();
                    changed = true;
                }

                if (EnsureInteractionValid(row.Interactions[i]))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private bool EnsureInteractionValid(NPCInteractionData interaction)
        {
            bool changed = false;

            if (interaction.Nodes == null)
            {
                interaction.Nodes = new List<NPCInteractionNodeData>();
                changed = true;
            }

            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                if (interaction.Nodes[i] == null)
                {
                    interaction.Nodes[i] = NPCInteractionNodeDataRegistry.Create(NPCInteractionNodeTypes.Dialogue);
                    changed = true;
                }

                if (EnsureNodeValid(interaction.Nodes[i]))
                {
                    changed = true;
                }
            }

            if (interaction.Nodes.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(interaction.EntryNodeGuid))
                {
                    interaction.EntryNodeGuid = string.Empty;
                    changed = true;
                }
            }
            else if (interaction.GetNode(interaction.EntryNodeGuid) == null)
            {
                interaction.EntryNodeGuid = interaction.Nodes[0]?.Guid;
                changed = true;
            }

            HashSet<string> validNodeGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                NPCInteractionNodeData node = interaction.Nodes[i];
                if (node != null && !string.IsNullOrWhiteSpace(node.Guid))
                {
                    validNodeGuids.Add(node.Guid);
                }
            }

            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                NPCInteractionNodeData node = interaction.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                node.Branches ??= new List<NPCInteractionBranchData>();
                for (int branchIndex = 0; branchIndex < node.Branches.Count; branchIndex++)
                {
                    NPCInteractionBranchData branch = node.Branches[branchIndex];
                    if (branch == null)
                    {
                        node.Branches[branchIndex] = new NPCInteractionBranchData();
                        changed = true;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(branch.NextNodeGuid) && !validNodeGuids.Contains(branch.NextNodeGuid))
                    {
                        branch.NextNodeGuid = string.Empty;
                        changed = true;
                    }
                }

                if (node is NPCSelectInteractionNodeData select)
                {
                    select.Options ??= new List<NPCSelectOptionData>();
                    for (int optionIndex = 0; optionIndex < select.Options.Count; optionIndex++)
                    {
                        NPCSelectOptionData option = select.Options[optionIndex];
                        if (option == null)
                        {
                            select.Options[optionIndex] = new NPCSelectOptionData();
                            changed = true;
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(option.NextNodeGuid) && !validNodeGuids.Contains(option.NextNodeGuid))
                        {
                            option.NextNodeGuid = string.Empty;
                            changed = true;
                        }
                    }
                }
            }

            return changed;
        }

        private static bool EnsureNodeValid(NPCInteractionNodeData node)
        {
            bool changed = false;

            string resolvedType = NPCInteractionNodeDataRegistry.ResolveTypeName(node);
            if (node.Type != resolvedType)
            {
                node.Type = resolvedType;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(node.Guid))
            {
                node.Guid = Guid.NewGuid().ToString("N");
                changed = true;
            }

            node.Branches ??= new List<NPCInteractionBranchData>();

            if (node is NPCMoveInteractionNodeData move && move.StopDistance < 0f)
            {
                move.StopDistance = 0f;
                changed = true;
            }

            if (node is NPCEnterDungeonInteractionNodeData enterDungeon && enterDungeon.DungeonFloor < 1)
            {
                enterDungeon.DungeonFloor = 1;
                changed = true;
            }

            if (node is NPCSelectInteractionNodeData select && select.Options == null)
            {
                select.Options = new List<NPCSelectOptionData>();
                changed = true;
            }

            return changed;
        }

        private static Color GetGraphNodeColor(NPCInteractionNodeData node)
        {
            return node switch
            {
                NPCDialogueInteractionNodeData => new Color(0.81f, 0.56f, 0.20f),
                NPCSelectInteractionNodeData => new Color(0.69f, 0.40f, 0.78f),
                NPCOpenUIInteractionNodeData => new Color(0.43f, 0.51f, 0.84f),
                NPCMoveInteractionNodeData => new Color(0.35f, 0.68f, 0.66f),
                NPCEnterDungeonInteractionNodeData => new Color(0.79f, 0.30f, 0.33f),
                NPCEnterTrainingGroundInteractionNodeData => new Color(0.30f, 0.56f, 0.81f),
                _ => new Color(0.52f, 0.52f, 0.52f),
            };
        }

        private static string GetListName(NPCData row)
        {
            if (!string.IsNullOrWhiteSpace(row.DisplayName))
            {
                return row.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(row.NPC))
            {
                return row.NPC;
            }

            return "Unnamed NPC";
        }
    }
}
