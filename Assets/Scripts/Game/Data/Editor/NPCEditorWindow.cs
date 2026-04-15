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
        private const float LabelWidth = 150f;
        private const float GraphNodeWidth = 460f;
        private const float GraphTerminalWidth = 240f;
        private const float GraphTerminalHeight = 28f;
        private const float GraphConnectorHeight = 24f;

        private List<NPCData> _rows = new();
        private bool _isDirty;
        private string _statusText = string.Empty;

        private int _selectedIndex = -1;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private class TableWrapper
        {
            public List<NPCData> Rows = new();
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

            for (int i = 0; i < _rows.Count; i++)
            {
                NPCData row = _rows[i];
                string label = $"{row.Id}  {GetListName(row)}";
                bool selected = i == _selectedIndex;
                if (GUILayout.Toggle(selected, label, "Button"))
                {
                    _selectedIndex = i;
                }
            }

            EditorGUILayout.EndScrollView();
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
            row.Id = EditorGUILayout.IntField("Id", row.Id);
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
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Node", GUILayout.Width(92f)))
            {
                ShowAddNodeMenu(interaction);
            }
            EditorGUILayout.EndHorizontal();

            DrawInteractionGraph(interaction);

            EditorGUILayout.EndVertical();
        }

        private void DrawInteractionGraph(NPCInteractionData interaction)
        {
            EditorGUILayout.Space(4f);
            DrawGraphTerminal("Start", new Color(0.23f, 0.49f, 0.76f));

            if (interaction.Nodes.Count == 0)
            {
                DrawGraphConnector();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("No node configured.", MessageType.None);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                DrawGraphConnector();
                DrawGraphTerminal("End", new Color(0.27f, 0.63f, 0.36f));
                return;
            }

            for (int i = 0; i < interaction.Nodes.Count; i++)
            {
                DrawGraphConnector();
                DrawGraphNode(interaction, i);
            }

            DrawGraphConnector();
            DrawGraphTerminal("End", new Color(0.27f, 0.63f, 0.36f));
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
            int maxId = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Id > maxId)
                {
                    maxId = _rows[i].Id;
                }
            }

            int id = maxId + 1;
            _rows.Add(new NPCData
            {
                Id = id,
                NPC = $"NPC_{id}",
                DisplayName = $"NPC {id}",
                Interactions = new List<NPCInteractionData>(),
            });
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
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
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

        private void ShowAddNodeMenu(NPCInteractionData interaction)
        {
            GenericMenu menu = new GenericMenu();
            foreach (string typeName in NPCInteractionNodeDataRegistry.TypeOrder)
            {
                string capturedTypeName = typeName;
                menu.AddItem(new GUIContent(NPCInteractionNodeDataRegistry.GetDisplayName(typeName)), false, () => AddNode(interaction, capturedTypeName));
            }

            menu.ShowAsContext();
        }

        private void AddNode(NPCInteractionData interaction, string typeName)
        {
            if (EnsureInteractionValid(interaction))
            {
                _isDirty = true;
            }

            NPCInteractionNodeData node = NPCInteractionNodeDataRegistry.Create(typeName);
            if (node == null)
            {
                return;
            }

            interaction.Nodes.Add(node);
            _isDirty = true;
            Repaint();
        }

        private void DrawGraphNode(NPCInteractionData interaction, int index)
        {
            NPCInteractionNodeData node = interaction.Nodes[index];
            if (node == null)
            {
                node = NPCInteractionNodeDataRegistry.Create(NPCInteractionNodeTypes.Dialogue);
                interaction.Nodes[index] = node;
                _isDirty = true;
            }

            if (EnsureNodeValid(node))
            {
                _isDirty = true;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical("box", GUILayout.Width(GraphNodeWidth));

            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, GetGraphNodeColor(node));
            GUI.Label(new Rect(headerRect.x + 8f, headerRect.y + 3f, headerRect.width - 16f, headerRect.height - 6f),
                $"{index + 1}. {NPCInteractionNodeDataRegistry.GetDisplayName(node.Type)}",
                EditorStyles.whiteBoldLabel);

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(42f));
            if (GUILayout.Button(NPCInteractionNodeDataRegistry.GetDisplayName(node.Type), EditorStyles.popup, GUILayout.Width(120f)))
            {
                ShowChangeNodeTypeMenu(interaction, index);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = index > 0;
            if (GUILayout.Button("Up", GUILayout.Width(52f)))
            {
                SwapNodes(interaction, index, index - 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            GUI.enabled = index < interaction.Nodes.Count - 1;
            if (GUILayout.Button("Down", GUILayout.Width(52f)))
            {
                SwapNodes(interaction, index, index + 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            GUI.enabled = true;
            if (GUILayout.Button("Delete", GUILayout.Width(56f)))
            {
                interaction.Nodes.RemoveAt(index);
                _isDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);

            EditorGUI.BeginChangeCheck();
            switch (node)
            {
                case NPCDialogueInteractionNodeData dialogue:
                    dialogue.Speaker = EditorGUILayout.TextField("Speaker", dialogue.Speaker ?? string.Empty);
                    dialogue.ContentKey = EditorGUILayout.TextField("Content Key", dialogue.ContentKey ?? string.Empty);
                    break;
                case NPCOpenUIInteractionNodeData openUI:
                    openUI.UIName = EditorGUILayout.TextField("UI Name", openUI.UIName ?? string.Empty);
                    openUI.OpenData = EditorGUILayout.TextField("Open Data", openUI.OpenData ?? string.Empty);
                    openUI.WaitUntilClosed = EditorGUILayout.Toggle("Wait Until Closed", openUI.WaitUntilClosed);
                    break;
                case NPCMoveInteractionNodeData move:
                    move.TargetMarker = EditorGUILayout.TextField("Target Marker", move.TargetMarker ?? string.Empty);
                    move.StopDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Stop Distance", move.StopDistance));
                    move.WaitUntilArrived = EditorGUILayout.Toggle("Wait Until Arrived", move.WaitUntilArrived);
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
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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

        private static void DrawGraphConnector()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, GraphConnectorHeight, GUILayout.ExpandWidth(true));
            Vector3 start = new Vector3(rect.center.x, rect.y + 2f, 0f);
            Vector3 end = new Vector3(rect.center.x, rect.yMax - 4f, 0f);
            Color color = new Color(0.55f, 0.62f, 0.72f);

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawLine(start, end);
            Vector3 arrowTip = end;
            Vector3 arrowLeft = arrowTip + new Vector3(-5f, -7f, 0f);
            Vector3 arrowRight = arrowTip + new Vector3(5f, -7f, 0f);
            Handles.DrawAAConvexPolygon(arrowTip, arrowLeft, arrowRight);
            Handles.EndGUI();
        }

        private void ShowChangeNodeTypeMenu(NPCInteractionData interaction, int index)
        {
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
            interaction.Nodes[index] = newNode;
            _isDirty = true;
            Repaint();
        }

        private void SwapNodes(NPCInteractionData interaction, int fromIndex, int toIndex)
        {
            NPCInteractionNodeData temp = interaction.Nodes[fromIndex];
            interaction.Nodes[fromIndex] = interaction.Nodes[toIndex];
            interaction.Nodes[toIndex] = temp;
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

            if (!string.IsNullOrWhiteSpace(interaction.ContentKey))
            {
                if (interaction.Nodes.Count == 0)
                {
                    interaction.Nodes.Add(new NPCDialogueInteractionNodeData
                    {
                        ContentKey = interaction.ContentKey,
                    });
                }

                interaction.ContentKey = string.Empty;
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

            if (node is NPCMoveInteractionNodeData move && move.StopDistance < 0f)
            {
                move.StopDistance = 0f;
                changed = true;
            }

            return changed;
        }

        private static Color GetGraphNodeColor(NPCInteractionNodeData node)
        {
            return node switch
            {
                NPCDialogueInteractionNodeData => new Color(0.81f, 0.56f, 0.20f),
                NPCOpenUIInteractionNodeData => new Color(0.43f, 0.51f, 0.84f),
                NPCMoveInteractionNodeData => new Color(0.35f, 0.68f, 0.66f),
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
