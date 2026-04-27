using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public class SkillEffectEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/SkillEffectDataTable.json";
        private const float ListPanelWidth = 220f;
        private const float ItemHeight = 26f;
        private const float InsertFieldWidth = 30f;
        private const float LabelWidth = 150f;

        private List<SkillEffectData> _rows = new();
        private bool _isDirty;
        private string _statusText = string.Empty;

        private int _selectedIndex = -1;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private readonly Dictionary<SkillEffectData, string> _insertTexts = new();

        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color SectionLine = new(0.45f, 0.45f, 0.45f, 1f);

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private class TableWrapper
        {
            public List<SkillEffectData> Rows = new();
        }

        [MenuItem("Tools/Data/Skill Effect Editor")]
        public static void Open()
        {
            SkillEffectEditorWindow window = GetWindow<SkillEffectEditorWindow>("Skill Effect Editor");
            window.minSize = new Vector2(920f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadData();
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"Missing file: {DataPath}";
                return;
            }

            try
            {
                string json = File.ReadAllText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                    _rows = wrapper.Rows;

                NormalizeRowIds();
                _insertTexts.Clear();
                _statusText = $"Loaded {_rows.Count} rows";
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                Debug.LogError($"[SkillEffectEditor] Load error:\n{ex}");
            }
        }

        private void SaveData()
        {
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
                _statusText = $"Saved {_rows.Count} rows";
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                Debug.LogError($"[SkillEffectEditor] Save error:\n{ex}");
            }
        }

        private void AddRow()
        {
            _rows.Add(new SkillEffectData
            {
                Id = _rows.Count + 1,
                Name = $"New Skill Effect {_rows.Count + 1}",
                Description = string.Empty,
                IconPath = string.Empty,
                Modifiers = new List<SkillModifierEntry>(),
            });

            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            SkillEffectData removedRow = _rows[_selectedIndex];
            _rows.RemoveAt(_selectedIndex);
            _insertTexts.Remove(removedRow);
            NormalizeRowIds();
            _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _rows.Count - 1);
            _isDirty = true;
        }

        private void DuplicateSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                return;

            SkillEffectData source = _rows[_selectedIndex];
            string json = JsonConvert.SerializeObject(source, JsonSettings);
            SkillEffectData copy = JsonConvert.DeserializeObject<SkillEffectData>(json, JsonSettings);
            if (copy == null)
                return;

            copy.Id = _rows.Count + 1;
            copy.Name = string.IsNullOrWhiteSpace(source.Name) ? $"Skill Effect {copy.Id}" : $"{source.Name}_Copy";
            copy.Modifiers ??= new List<SkillModifierEntry>();
            _rows.Add(copy);
            NormalizeRowIds();
            _selectedIndex = _rows.Count - 1;
            _isDirty = true;
            Repaint();
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

            SkillEffectData row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);
            insertIndex = Mathf.Clamp(insertIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);
            NormalizeRowIds();
            _selectedIndex = insertIndex;
            _isDirty = true;
            GUI.FocusControl(null);
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawListPanel();
            DrawPanelDivider();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                LoadData();

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                SaveData();
            GUI.enabled = true;

            if (GUILayout.Button("+ Add", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                AddRow();

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                DuplicateSelected();

            GUI.color = _selectedIndex >= 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                if (EditorUtility.DisplayDialog("Delete Skill Effect", $"Delete {_rows[_selectedIndex].Name}?", "Delete", "Cancel"))
                    DeleteSelected();
            }
            GUI.color = Color.white;
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Skill Effects ({_rows.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event evt = Event.current;
            SkillEffectData moveRow = null;
            int moveToIndex = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                SkillEffectData row = _rows[i];
                bool isSelected = i == _selectedIndex;
                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color bg = isSelected ? SelectedColor : itemRect.Contains(evt.mousePosition) ? HoverColor : i % 2 == 0 ? EvenRowColor : OddRowColor;
                EditorGUI.DrawRect(itemRect, bg);

                Rect insertRect = new(itemRect.x + 6f, itemRect.y + 3f, InsertFieldWidth, itemRect.height - 6f);
                string insertText = _insertTexts.TryGetValue(row, out string currentInsertText) ? currentInsertText : string.Empty;
                string controlName = $"insert_{row.GetHashCode()}";
                GUI.SetNextControlName(controlName);
                string newInsertText = EditorGUI.TextField(insertRect, insertText);
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

                string label = $"[{row.Id}] {(string.IsNullOrWhiteSpace(row.Name) ? "Unnamed" : row.Name)}";
                GUI.Label(new Rect(insertRect.xMax + 8f, itemRect.y + 4f, itemRect.width - insertRect.width - 12f, itemRect.height - 4f),
                    label, isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition) && !insertRect.Contains(evt.mousePosition))
                {
                    _selectedIndex = i;
                    GUI.FocusControl(null);
                    evt.Use();
                    Repaint();
                }

                if (evt.type == EventType.MouseMove)
                    Repaint();
            }

            EditorGUILayout.EndScrollView();
            if (moveRow != null)
                MoveRowToInsertIndex(_rows.IndexOf(moveRow), moveToIndex);

            EditorGUILayout.EndVertical();
        }

        private void DrawPanelDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, DividerColor);
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a skill effect on the left", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            SkillEffectData row = _rows[_selectedIndex];
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"[{row.Id}] {row.Name}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUI.BeginChangeCheck();
            DrawSectionHeader("Basic");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("Id", row.Id);
            row.Name = EditorGUILayout.TextField("Name", row.Name ?? string.Empty);
            row.IconPath = EditorGUILayout.TextField("Icon Path", row.IconPath ?? string.Empty);
            EditorGUILayout.LabelField("Description");
            row.Description = EditorGUILayout.TextArea(row.Description ?? string.Empty, GUILayout.MinHeight(48f), GUILayout.MaxHeight(80f));
            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            DrawSectionHeader("Modifiers");
            DrawModifierList(row);

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawModifierList(SkillEffectData row)
        {
            row.Modifiers ??= new List<SkillModifierEntry>();

            int removeAt = -1;
            for (int i = 0; i < row.Modifiers.Count; i++)
            {
                SkillModifierEntry entry = row.Modifiers[i];

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                entry.Channel = (SkillModifierChannel)EditorGUILayout.EnumPopup(entry.Channel, GUILayout.MinWidth(180f));

                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 46f;
                entry.Factor = EditorGUILayout.FloatField("Factor", entry.Factor, GUILayout.MinWidth(90f));
                entry.Bonus = EditorGUILayout.FloatField("Bonus", entry.Bonus, GUILayout.MinWidth(90f));
                EditorGUIUtility.labelWidth = previousLabelWidth;

                if (GUILayout.Button("Delete", GUILayout.Width(52f)))
                    removeAt = i;

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    row.Modifiers[i] = entry;
                    _isDirty = true;
                }
            }

            if (GUILayout.Button("+ Add Modifier", GUILayout.Width(120f)))
            {
                row.Modifiers.Add(new SkillModifierEntry());
                _isDirty = true;
            }

            if (removeAt >= 0)
            {
                row.Modifiers.RemoveAt(removeAt);
                _isDirty = true;
            }
        }

        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height + 1f;
            rect.height = 1f;
            EditorGUI.DrawRect(rect, SectionLine);
            GUILayout.Space(4f);
        }
    }
}
