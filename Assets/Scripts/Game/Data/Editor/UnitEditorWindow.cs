using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Core;
using CrystalMagic.Editor.Unit;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public class UnitEditorWindow : EditorWindow
    {
        private const string DataPath = "Assets/Res/Data/UnitDataTable.json";
        private const string DropDataPath = "Assets/Res/Data/DropDataTable.json";
        private const string AnimationProfileDataPath = "Assets/Res/Data/UnitAnimationProfileDataTable.json";
        private const string UnitPrefabDirectory = "Assets/Res/Prefab/Unit";
        private const string AnimationClipDirectory = "Assets/Res/Data/UnitAnimationClips";
        private const float ListPanelWidth = 220f;
        private const float ItemHeight = 26f;
        private const float LabelWidth = 140f;

        private sealed class UnitPrefabEntry
        {
            public string AssetPath;
            public GameObject Prefab;

            public string DisplayName => Path.GetFileNameWithoutExtension(AssetPath);
        }

        private sealed class TableWrapper
        {
            public List<UnitData> Rows = new();
        }

        private sealed class DropTableWrapper
        {
            public List<DropData> Rows = new();
        }

        private sealed class AnimationProfileTableWrapper
        {
            public List<UnitAnimationProfileData> Rows = new();
        }

        private sealed class IntOption
        {
            public int Id;
            public string Label;
        }

        private static readonly Color SelectedColor = new(0.27f, 0.52f, 0.85f, 0.85f);
        private static readonly Color EvenRowColor = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color OddRowColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color HoverColor = new(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color SectionLine = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DividerColor = new(0.15f, 0.15f, 0.15f, 1f);

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private List<UnitData> _rows = new();
        private readonly List<DropData> _dropRows = new();
        private readonly List<UnitAnimationProfileData> _animationProfiles = new();
        private readonly List<UnitPrefabEntry> _prefabEntries = new();
        private bool _isDirty;
        private string _statusText = string.Empty;
        private int _selectedIndex = -1;
        private int _selectedTab;
        private int _copySourceUnitIndex;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;

        [MenuItem("Tools/Data/Unit Editor")]
        public static void Open()
        {
            UnitEditorWindow window = GetWindow<UnitEditorWindow>("Unit Editor");
            window.minSize = new Vector2(900f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadData();
            LoadDropData();
            LoadAnimationProfiles();
            RefreshPrefabEntries();
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

        internal static void MarkPrefabDirty(UnityEngine.Object target)
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }

        internal void MarkDirty()
        {
            _isDirty = true;
        }

        internal static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height + 1f;
            rect.height = 1f;
            EditorGUI.DrawRect(rect, SectionLine);
            GUILayout.Space(4f);
        }

        private void RefreshPrefabEntries()
        {
            _prefabEntries.Clear();
            if (!AssetDatabase.IsValidFolder(UnitPrefabDirectory))
            {
                _selectedIndex = -1;
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitPrefabDirectory });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                _prefabEntries.Add(new UnitPrefabEntry
                {
                    AssetPath = path,
                    Prefab = prefab,
                });
            }

            _prefabEntries.Sort((left, right) =>
            {
                int leftId = ResolveUnitData(left)?.Id ?? int.MaxValue;
                int rightId = ResolveUnitData(right)?.Id ?? int.MaxValue;
                int idComparison = leftId.CompareTo(rightId);
                return idComparison != 0
                    ? idComparison
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            });
            _selectedIndex = _prefabEntries.Count == 0 ? -1 : Mathf.Clamp(_selectedIndex, 0, _prefabEntries.Count - 1);
        }

        private bool TryGetSelectedPrefabEntry(out UnitPrefabEntry entry)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _prefabEntries.Count)
            {
                entry = _prefabEntries[_selectedIndex];
                return true;
            }

            entry = null;
            return false;
        }

        private UnitData ResolveUnitData(UnitPrefabEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            UnitData byPath = _rows.FirstOrDefault(row => string.Equals(row.PrefabPath, entry.AssetPath, StringComparison.Ordinal));
            if (byPath != null)
            {
                return byPath;
            }

            return _rows.FirstOrDefault(row => string.Equals(row.Name, entry.DisplayName, StringComparison.Ordinal));
        }

        private UnitData CreateUnitDataForPrefab(UnitPrefabEntry entry)
        {
            UnitData row = new UnitData
            {
                Id = GetNextStableUnitDataId(),
                Name = entry.DisplayName,
                Description = string.Empty,
                PrefabPath = entry.AssetPath,
            };
            row.NormalizeModules();
            _rows.Add(row);
            _isDirty = true;
            return row;
        }

        private UnitData CreateUnitDataForPrefab(UnitPrefabEntry entry, UnitData source)
        {
            if (source == null)
            {
                return CreateUnitDataForPrefab(entry);
            }

            string json = JsonConvert.SerializeObject(source, JsonSettings);
            UnitData row = JsonConvert.DeserializeObject<UnitData>(json, JsonSettings);
            if (row == null)
            {
                return CreateUnitDataForPrefab(entry);
            }

            row.Name = entry.DisplayName;
            row.PrefabPath = entry.AssetPath;
            row.Id = GetNextStableUnitDataId();
            row.NormalizeModules();
            _rows.Add(row);
            _isDirty = true;
            return row;
        }

        private static bool HasAuthoring<T>(UnitPrefabEntry entry) where T : Component
        {
            return entry?.Prefab != null && entry.Prefab.GetComponent<T>() != null;
        }

        private void LoadData()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(DataPath))
            {
                _statusText = $"未找到文件：{DataPath}，将新建";
                return;
            }

            try
            {
                string json = DataFileUtility.ReadJsonText(DataPath);
                TableWrapper wrapper = JsonConvert.DeserializeObject<TableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                {
                    _rows = wrapper.Rows;
                }

                foreach (UnitData row in _rows)
                {
                    row?.NormalizeModules();
                }

                _statusText = $"Loaded {_rows.Count} rows - {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"加载失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Load error:\n{ex}");
            }

        }
        private void SaveData()
        {
            string directory = Path.GetDirectoryName(DataPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            try
            {
                RefreshPrefabEntries();
                List<UnitData> saveRows = BuildSaveRowsFromPrefabs();
                int removedCount = Mathf.Max(0, _rows.Count - saveRows.Count);
                _rows = saveRows;

                foreach (UnitData row in _rows)
                {
                    row?.NormalizeModules();
                }

                string json = JsonConvert.SerializeObject(new TableWrapper { Rows = _rows }, JsonSettings);
                DataFileUtility.WriteJsonText(DataPath, json);
                SaveDropData();
                SaveAnimationProfiles();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = removedCount > 0
                    ? $"已保存 {_rows.Count} 条，清理 {removedCount} 条旧数据 - {DataPath}"
                    : $"已保存 {_rows.Count} 条 - {DataPath}";
            }
            catch (Exception ex)
            {
                _statusText = $"保存失败：{ex.Message}";
                Debug.LogError($"[UnitEditor] Save error:\n{ex}");
            }
        }

        private void LoadDropData()
        {
            _dropRows.Clear();
            if (!File.Exists(DropDataPath))
                return;

            try
            {
                string json = DataFileUtility.ReadJsonText(DropDataPath);
                DropTableWrapper wrapper = JsonConvert.DeserializeObject<DropTableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                    _dropRows.AddRange(wrapper.Rows);

                NormalizeDropRowIds();
                foreach (DropData row in _dropRows)
                    row?.EnsureValid();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnitEditor] Drop table load error:\n{ex}");
            }
        }

        private void SaveDropData()
        {
            string directory = Path.GetDirectoryName(DropDataPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            NormalizeDropRowIds();
            foreach (DropData row in _dropRows)
                row?.EnsureValid();

            string json = JsonConvert.SerializeObject(new DropTableWrapper { Rows = _dropRows }, JsonSettings);
            DataFileUtility.WriteJsonText(DropDataPath, json);
        }

        private void LoadAnimationProfiles()
        {
            _animationProfiles.Clear();
            if (!File.Exists(AnimationProfileDataPath))
                return;

            try
            {
                string json = DataFileUtility.ReadJsonText(AnimationProfileDataPath);
                AnimationProfileTableWrapper wrapper = JsonConvert.DeserializeObject<AnimationProfileTableWrapper>(json, JsonSettings);
                if (wrapper?.Rows != null)
                    _animationProfiles.AddRange(wrapper.Rows);

                NormalizeAnimationProfiles();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnitEditor] Animation profile load error:\n{ex}");
            }
        }

        private void SaveAnimationProfiles()
        {
            string directory = Path.GetDirectoryName(AnimationProfileDataPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            SynchronizeAnimationProfiles();
            string json = JsonConvert.SerializeObject(
                new AnimationProfileTableWrapper { Rows = _animationProfiles },
                JsonSettings);
            DataFileUtility.WriteJsonText(AnimationProfileDataPath, json);
            UnitAnimationFrameLibraryBuilder.Rebuild(_animationProfiles);
        }

        private void SynchronizeAnimationProfiles()
        {
            for (int i = _animationProfiles.Count - 1; i >= 0; i--)
            {
                UnitAnimationProfileData profile = _animationProfiles[i];
                if (profile == null)
                {
                    _animationProfiles.RemoveAt(i);
                    continue;
                }

                UnitData unit = _rows.FirstOrDefault(row =>
                    row != null &&
                    (!string.IsNullOrWhiteSpace(profile.UnitName)
                        ? string.Equals(row.Name, profile.UnitName, StringComparison.Ordinal)
                        : row.Id == profile.UnitDataId));
                if (unit == null)
                {
                    _animationProfiles.RemoveAt(i);
                    continue;
                }

                profile.UnitDataId = unit.Id;
                profile.UnitName = unit.Name;
                profile.Normalize();
            }

            NormalizeAnimationProfiles();
        }

        private void NormalizeAnimationProfiles()
        {
            for (int i = 0; i < _animationProfiles.Count; i++)
            {
                _animationProfiles[i] ??= new UnitAnimationProfileData();
                _animationProfiles[i].Id = i;
                _animationProfiles[i].Normalize();
            }
        }

        private List<UnitData> BuildSaveRowsFromPrefabs()
        {
            List<UnitData> rows = new();
            HashSet<UnitData> usedRows = new();

            for (int i = 0; i < _prefabEntries.Count; i++)
            {
                UnitPrefabEntry entry = _prefabEntries[i];
                UnitData row = ResolveUnitData(entry);
                if (row == null || !usedRows.Add(row))
                {
                    continue;
                }

                row.Name = entry.DisplayName;
                row.PrefabPath = entry.AssetPath;
                row.NormalizeModules();
                rows.Add(row);
            }

            return rows.OrderBy(static row => row.Id).ToList();
        }

        private int GetNextStableUnitDataId()
        {
            return _rows.Count == 0 ? 1 : _rows.Max(static row => row.Id) + 1;
        }

        private void NormalizeDropRowIds()
        {
            for (int i = 0; i < _dropRows.Count; i++)
                _dropRows[i].Id = i;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                SaveData();
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
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Prefab 列表 ({_prefabEntries.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));
            Event currentEvent = Event.current;

            for (int i = 0; i < _prefabEntries.Count; i++)
            {
                UnitPrefabEntry entry = _prefabEntries[i];
                UnitData unitData = ResolveUnitData(entry);
                bool isSelected = i == _selectedIndex;
                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, ItemHeight, GUILayout.ExpandWidth(true));

                Color backgroundColor = isSelected
                    ? SelectedColor
                    : itemRect.Contains(currentEvent.mousePosition)
                        ? HoverColor
                        : i % 2 == 0
                            ? EvenRowColor
                            : OddRowColor;
                EditorGUI.DrawRect(itemRect, backgroundColor);

                string bindingLabel = unitData == null
                    ? "(未绑定 UnitData)"
                    : $"[{unitData.Id}] {unitData.Name}";
                string label = $"{entry.DisplayName}  {bindingLabel}";
                GUI.Label(
                    new Rect(itemRect.x + 8f, itemRect.y + 4f, itemRect.width - 16f, itemRect.height - 4f),
                    label,
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (currentEvent.type == EventType.MouseDown && itemRect.Contains(currentEvent.mousePosition))
                {
                    CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    _selectedIndex = i;
                    currentEvent.Use();
                    Repaint();
                }

                if (currentEvent.type == EventType.MouseMove)
                {
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();
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

            if (!TryGetSelectedPrefabEntry(out UnitPrefabEntry entry))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("从左侧选择一个 Prefab", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            UnitData unit = ResolveUnitData(entry);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(entry.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            DrawBindingPanel(entry, ref unit);

            if (unit == null)
            {
                EditorGUILayout.HelpBox("当前 Prefab 还没有匹配到 UnitData，可以直接创建，或者从已有数据复制一份。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            string[] tabs = { "属性", "动画" };
            _selectedTab = Mathf.Clamp(_selectedTab, 0, tabs.Length - 1);
            int newTab = GUILayout.Toolbar(_selectedTab, tabs, GUILayout.Width(260f), GUILayout.Height(24f));
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2f);
            Rect lineRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, SectionLine);
            GUILayout.Space(4f);

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            switch (_selectedTab)
            {
                case 0:
                    DrawAttributePanel(entry, unit);
                    break;
                case 1:
                    DrawAnimationPanel(entry, unit);
                    break;
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBindingPanel(UnitPrefabEntry entry, ref UnitData unit)
        {
            DrawSectionHeader("Prefab 绑定");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Prefab", entry.AssetPath);
            }

            if (unit != null)
            {
                EditorGUILayout.LabelField("绑定方式", "按 PrefabPath 自动匹配");
                EditorGUILayout.LabelField("当前数据", $"[{unit.Id}] {unit.Name}");
            }
            else
            {
                EditorGUILayout.HelpBox("还没有与这个 PrefabPath 对应的 UnitData。", MessageType.Info);
            }

            if (unit == null && GUILayout.Button("为当前 Prefab 创建 UnitData", GUILayout.Width(180f)))
            {
                unit = CreateUnitDataForPrefab(entry);
            }

            if (unit == null && _rows.Count > 0)
            {
                string[] options = _rows.Select(row => $"[{row.Id}] {row.Name}").ToArray();
                _copySourceUnitIndex = Mathf.Clamp(_copySourceUnitIndex, 0, options.Length - 1);
                _copySourceUnitIndex = EditorGUILayout.Popup("复制来源", _copySourceUnitIndex, options);

                if (GUILayout.Button("复制已有 UnitData 生成", GUILayout.Width(180f)))
                {
                    unit = CreateUnitDataForPrefab(entry, _rows[_copySourceUnitIndex]);
                }
            }
        }

        private void DrawAttributePanel(UnitPrefabEntry entry, UnitData unit)
        {
            EditorGUI.BeginChangeCheck();

            DrawSectionHeader("基础信息");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Id", unit.Id);
            }

            unit.Name = EditorGUILayout.TextField("名称", unit.Name ?? string.Empty);
            EditorGUILayout.LabelField("描述");
            unit.Description = EditorGUILayout.TextArea(unit.Description ?? string.Empty, GUILayout.MinHeight(48f), GUILayout.MaxHeight(80f));

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("预制体路径", entry.AssetPath);
            }

            if (unit.PrefabPath != entry.AssetPath)
            {
                unit.PrefabPath = entry.AssetPath;
                _isDirty = true;
            }

            UnitEditorDrawerContext context = new(this, entry.Prefab, entry.AssetPath, entry.DisplayName, unit);
            IReadOnlyList<IUnitEditorAttributeDrawer> drawers = UnitEditorAttributeDrawerFactory.GetDrawers();
            foreach (IUnitEditorAttributeDrawer drawer in drawers)
            {
                if (drawer.CanDraw(context))
                {
                    drawer.Draw(context);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                unit.NormalizeModules();
                _isDirty = true;
            }
        }

        private void DrawAnimationPanel(UnitPrefabEntry entry, UnitData unit)
        {
            DrawSectionHeader("Animation");
            if (entry.Prefab.GetComponent<UnitAnimationAuthoring>() == null)
            {
                EditorGUILayout.HelpBox("当前 Prefab 没有 UnitAnimationAuthoring，不能配置单位动画。", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Prefab 必须手动挂载 SpriteRenderer；运行时动画直接从 Entity 获取该组件。",
                MessageType.None);

            UnitAnimationProfileData profile = GetAnimationProfile(unit);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("当前单位还没有动画配置。", MessageType.Info);
                if (GUILayout.Button("创建动画配置", GUILayout.Width(160f)))
                {
                    profile = new UnitAnimationProfileData
                    {
                        UnitDataId = unit.Id,
                        UnitName = unit.Name,
                    };
                    _animationProfiles.Add(profile);
                    NormalizeAnimationProfiles();
                    _isDirty = true;
                }

                return;
            }

            if (GUILayout.Button("删除动画配置", GUILayout.Width(160f)))
            {
                _animationProfiles.Remove(profile);
                NormalizeAnimationProfiles();
                _isDirty = true;
                return;
            }

            profile.UnitDataId = unit.Id;
            profile.UnitName = unit.Name;
            profile.Normalize();

            EditorGUILayout.LabelField("Unit Data", $"[{unit.Id}] {unit.Name}");
            EditorGUILayout.HelpBox(
                "StateScript writes unit.animation.setName. 每组动画可选 2 方向或 4 方向；2 方向只使用左右 Clip，上下移动会保持最近一次的左右朝向。",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < profile.Animations.Count; i++)
            {
                UnitAnimationEntryData animation = profile.Animations[i];
                if (animation == null)
                {
                    animation = new UnitAnimationEntryData();
                    profile.Animations[i] = animation;
                }

                DrawAnimationEntry(entry, profile, animation, i);
                if (i >= profile.Animations.Count)
                    break;
            }

            if (GUILayout.Button("新增动画", GUILayout.Width(120f)))
            {
                profile.Animations.Add(new UnitAnimationEntryData
                {
                    Name = "NewAnimation",
                });
                _isDirty = true;
            }

            if (EditorGUI.EndChangeCheck())
            {
                profile.Normalize();
                _isDirty = true;
            }
        }

        private void DrawAnimationEntry(
            UnitPrefabEntry entry,
            UnitAnimationProfileData profile,
            UnitAnimationEntryData animation,
            int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Animation {index + 1}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("删除", GUILayout.Width(60f)))
            {
                profile.Animations.RemoveAt(index);
                _isDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            animation.Name = EditorGUILayout.TextField("Animation Name", animation.Name ?? string.Empty);
            int directionModeIndex = animation.DirectionMode == UnitAnimationDirectionMode.TwoDirections ? 0 : 1;
            directionModeIndex = EditorGUILayout.Popup("方向", directionModeIndex, new[] { "2 方向（左右）", "4 方向（上下左右）" });
            animation.DirectionMode = directionModeIndex == 0
                ? UnitAnimationDirectionMode.TwoDirections
                : UnitAnimationDirectionMode.FourDirections;

            DrawAnimationClipField(entry, animation.Name, "Left", "Left", ref animation.LeftClipPath);
            DrawAnimationClipField(entry, animation.Name, "Right", "Right", ref animation.RightClipPath);
            if (animation.DirectionMode == UnitAnimationDirectionMode.FourDirections)
            {
                DrawAnimationClipField(entry, animation.Name, "Down / Front", "Front", ref animation.FrontClipPath);
                DrawAnimationClipField(entry, animation.Name, "Up / Back", "Back", ref animation.BackClipPath);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private static void DrawAnimationClipField(
            UnitPrefabEntry entry,
            string animationName,
            string label,
            string directionName,
            ref string path)
        {
            AnimationClip current = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            EditorGUILayout.BeginHorizontal();
            AnimationClip next = (AnimationClip)EditorGUILayout.ObjectField(label, current, typeof(AnimationClip), false);
            if (next != current)
            {
                path = next != null ? AssetDatabase.GetAssetPath(next) : string.Empty;
                GUI.changed = true;
            }

            if (GUILayout.Button("新建", GUILayout.Width(50f)))
            {
                AnimationClip clip = CreateAnimationClip(entry, animationName, directionName);
                path = AssetDatabase.GetAssetPath(clip);
                Selection.activeObject = clip;
                EditorGUIUtility.PingObject(clip);
                GUI.changed = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static AnimationClip CreateAnimationClip(UnitPrefabEntry entry, string animationName, string directionName)
        {
            EnsureAnimationClipDirectory();
            string unitName = SanitizeFileName(entry?.DisplayName ?? "Unit");
            string clipName = SanitizeFileName(string.IsNullOrWhiteSpace(animationName) ? "Animation" : animationName);
            string direction = SanitizeFileName(directionName);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{AnimationClipDirectory}/{unitName}_{clipName}_{direction}.anim");
            AnimationClip clip = new() { frameRate = 12f };
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            return clip;
        }

        private static void EnsureAnimationClipDirectory()
        {
            string[] segments = AnimationClipDirectory.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter, '_');
            return string.IsNullOrWhiteSpace(value) ? "Animation" : value;
        }

        private UnitAnimationProfileData GetAnimationProfile(UnitData unit)
        {
            if (unit == null)
                return null;

            return _animationProfiles.FirstOrDefault(profile => profile != null && profile.UnitDataId == unit.Id);
        }

        private static string GetBehaviorTreePreviewName(BehaviorTreeData tree)
        {
            if (!string.IsNullOrWhiteSpace(tree?.Name))
            {
                return tree.Name;
            }

            return "Unnamed Tree";
        }

        internal DropData GetDropData(int dropDataId)
        {
            if (dropDataId < 0)
                return null;

            for (int i = 0; i < _dropRows.Count; i++)
            {
                DropData row = _dropRows[i];
                if (row != null && row.Id == dropDataId)
                    return row;
            }

            return null;
        }

        internal DropData CreateDropDataForUnit(UnitData unit, UnitDropModuleData module)
        {
            if (unit == null || module == null)
                return null;

            DropData row = new DropData
            {
                Id = _dropRows.Count,
                Name = string.IsNullOrWhiteSpace(unit.Name) ? $"Drop {_dropRows.Count}" : $"{unit.Name} Drop",
                Description = string.Empty,
                Entries = new List<DropEntryData>(),
            };
            row.EnsureValid();
            _dropRows.Add(row);
            NormalizeDropRowIds();
            module.DropDataId = row.Id;
            _isDirty = true;
            return row;
        }

        internal bool DrawInlineDropDataEditor(UnitData unit, UnitDropModuleData module)
        {
            if (module == null)
                return false;

            bool changed = false;
            List<IntOption> dropOptions = BuildDropOptions(_dropRows);
            EditorGUILayout.BeginHorizontal();
            int newDropDataId = DrawIntPopup("Drop Table", module.DropDataId, dropOptions);
            if (newDropDataId != module.DropDataId)
            {
                module.DropDataId = newDropDataId;
                changed = true;
            }

            if (GUILayout.Button("Create", GUILayout.Width(72f)))
            {
                DropData created = CreateDropDataForUnit(unit, module);
                if (created != null)
                    changed = true;
            }
            EditorGUILayout.EndHorizontal();

            DropData dropData = GetDropData(module.DropDataId);
            if (module.DropDataId >= 0 && dropData == null)
            {
                EditorGUILayout.HelpBox($"Missing DropData #{module.DropDataId}. Create a new one or switch the reference.", MessageType.Warning);
                return changed;
            }

            if (dropData == null)
            {
                EditorGUILayout.HelpBox("No drop table assigned. Click Create to make one for this unit, or pick an existing table.", MessageType.Info);
                return changed;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");
            dropData.Name = EditorGUILayout.TextField("Table Name", dropData.Name ?? string.Empty);
            EditorGUILayout.LabelField("Description");
            dropData.Description = EditorGUILayout.TextArea(dropData.Description ?? string.Empty, GUILayout.MinHeight(36f), GUILayout.MaxHeight(72f));

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Entry", GUILayout.Width(84f)))
            {
                dropData.Entries.Add(new DropEntryData());
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            dropData.Entries ??= new List<DropEntryData>();
            List<IntOption> itemOptions = BuildItemOptions();
            for (int i = 0; i < dropData.Entries.Count; i++)
            {
                DropEntryData entry = dropData.Entries[i] ?? new DropEntryData();
                dropData.Entries[i] = entry;

                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    dropData.Entries.RemoveAt(i);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                entry.DropType = (DropRewardType)EditorGUILayout.EnumPopup("Drop Type", entry.DropType);
                if (entry.DropType == DropRewardType.Item)
                    entry.ItemId = DrawIntPopup("Item", entry.ItemId, itemOptions);
                else
                    EditorGUILayout.HelpBox("Money reward does not require an item id.", MessageType.None);

                entry.Chance = EditorGUILayout.Slider("Chance", entry.Chance, 0f, 1f);
                entry.MinQuantity = Mathf.Max(0, EditorGUILayout.IntField("Min Quantity", entry.MinQuantity));
                entry.MaxQuantity = Mathf.Max(entry.MinQuantity, EditorGUILayout.IntField("Max Quantity", entry.MaxQuantity));
                EditorGUILayout.EndVertical();
            }

            dropData.EnsureValid();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
            return true;
        }

        private static List<IntOption> BuildDropOptions(IEnumerable<DropData> dropRows)
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
            };

            if (dropRows != null)
            {
                foreach (DropData row in dropRows.OrderBy(static row => row.Id))
                {
                    if (row == null)
                        continue;

                    options.Add(new IntOption
                    {
                        Id = row.Id,
                        Label = $"[{row.Id}] {row.Name}",
                    });
                }
            }

            return options;
        }

        private static List<IntOption> BuildItemOptions()
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
            };

            foreach (ItemData row in EditorComponents.Data.FindAll<ItemData>(static _ => true).OrderBy(static row => row.Id))
            {
                options.Add(new IntOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private static int DrawIntPopup(string label, int currentId, List<IntOption> options)
        {
            int selectedIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Id == currentId)
                {
                    selectedIndex = i;
                    break;
                }
            }

            string[] labels = options.Select(static option => option.Label).ToArray();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            return newIndex >= 0 && newIndex < options.Count ? options[newIndex].Id : currentId;
        }
    }
}
