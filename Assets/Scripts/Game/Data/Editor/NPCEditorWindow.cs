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
            EnsureNpcValid(row);

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
            interaction.ContentKey = EditorGUILayout.TextField("Content Key", interaction.ContentKey ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                _isDirty = true;
            }

            EditorGUILayout.EndVertical();
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
                    EnsureNpcValid(_rows[i]);
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
            EnsureNpcValid(row);
            row.Interactions.Add(new NPCInteractionData
            {
                Key = $"Interaction_{row.Interactions.Count + 1}",
                DisplayName = $"Interaction {row.Interactions.Count + 1}",
                EnableExpression = string.Empty,
                ContentKey = string.Empty,
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

        private static void EnsureNpcValid(NPCData row)
        {
            row.Interactions ??= new List<NPCInteractionData>();
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
