using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public class UnitAnimationProfileEditorWindow : EditorWindow
    {
        private const string ProfileDataPath = "Assets/Res/Data/UnitAnimationProfileDataTable.json";
        private const string UnitDataPath = "Assets/Res/Data/UnitDataTable.json";
        private const string UnitPrefabDirectory = "Assets/Res/Prefab/Unit";
        private const string SpriteClipDirectory = "Assets/Res/Data/UnitAnimationClips";
        private const float ListPanelWidth = 260f;
        private static readonly string[] KnownStateNames = { "IdleState", "MoveState", "ControlledState", "UnitCastState", "PlayerCastState", "DeathState" };

        private sealed class UnitPrefabEntry
        {
            public string AssetPath;
            public GameObject Prefab;

            public string DisplayName => Path.GetFileNameWithoutExtension(AssetPath);
        }

        private sealed class ProfileTableWrapper
        {
            public List<UnitAnimationProfileData> Rows = new();
        }

        private sealed class UnitTableWrapper
        {
            public List<UnitData> Rows = new();
        }

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private readonly List<UnitAnimationProfileData> _rows = new();
        private readonly List<UnitData> _units = new();
        private readonly List<UnitPrefabEntry> _prefabEntries = new();
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private int _selectedIndex = -1;
        private int _copySourceProfileIndex;
        private bool _isDirty;
        private string _statusText = string.Empty;
        private string _searchText = string.Empty;

        [MenuItem("Tools/Data/Unit Animation Editor")]
        public static void Open()
        {
            UnitAnimationProfileEditorWindow window = GetWindow<UnitAnimationProfileEditorWindow>("Unit Animation Editor");
            window.minSize = new Vector2(1080f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadAll();
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
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                LoadAll();
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                SaveData();
            if (GUILayout.Button("Refresh Prefab", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                RefreshPrefabEntries();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_statusText, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Prefab List ({_prefabEntries.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            _searchText = EditorGUILayout.TextField("Search", _searchText ?? string.Empty);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            Event currentEvent = Event.current;
            for (int i = 0; i < _prefabEntries.Count; i++)
            {
                UnitPrefabEntry entry = _prefabEntries[i];
                if (!string.IsNullOrWhiteSpace(_searchText) &&
                    entry.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                UnitAnimationProfileData profile = ResolveProfile(entry);
                bool isSelected = i == _selectedIndex;
                Rect itemRect = GUILayoutUtility.GetRect(ListPanelWidth, 26f, GUILayout.ExpandWidth(true));
                Color backgroundColor = isSelected
                    ? new Color(0.27f, 0.52f, 0.85f, 0.85f)
                    : itemRect.Contains(currentEvent.mousePosition)
                        ? new Color(0.32f, 0.32f, 0.32f, 1f)
                        : i % 2 == 0
                            ? new Color(0.22f, 0.22f, 0.22f, 1f)
                            : new Color(0.25f, 0.25f, 0.25f, 1f);
                EditorGUI.DrawRect(itemRect, backgroundColor);

                string bindingLabel = profile == null
                    ? "(No Animation Data)"
                    : $"{profile.Animations.Count} clips";
                GUI.Label(
                    new Rect(itemRect.x + 8f, itemRect.y + 4f, itemRect.width - 16f, itemRect.height - 8f),
                    $"{entry.DisplayName}  {bindingLabel}",
                    isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

                if (currentEvent.type == EventType.MouseDown && itemRect.Contains(currentEvent.mousePosition))
                {
                    _selectedIndex = i;
                    currentEvent.Use();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            if (!TryGetSelectedPrefabEntry(out UnitPrefabEntry entry))
            {
                EditorGUILayout.HelpBox("Select a prefab on the left.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            UnitData unitData = ResolveUnitData(entry);
            UnitAnimationProfileData profile = ResolveProfile(entry);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(entry.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            DrawBindingPanel(entry, unitData, ref profile);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("This prefab has no animation data yet. Create one on the panel above.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            profile.Normalize();

            EditorGUILayout.HelpBox(
                "Rule: regular states use the default animation whose Animation Name is empty. Skill casts match by skill display name and Animation Name.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            DrawUnitInfo(entry, unitData, profile);
            profile.PlaybackSpeed = Mathf.Max(0.01f, EditorGUILayout.FloatField("Playback Speed", profile.PlaybackSpeed));

            DrawSectionHeader("Animations");
            if (GUILayout.Button("Add Animation", GUILayout.Width(160f)))
            {
                profile.Animations.Add(new UnitAnimationEntryData());
                _isDirty = true;
            }

            for (int i = 0; i < profile.Animations.Count; i++)
            {
                UnitAnimationEntryData animation = profile.Animations[i] ??= new UnitAnimationEntryData();
                animation.Normalize();
                DrawAnimationEntry(unitData, entry, profile, i, animation);
            }

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBindingPanel(UnitPrefabEntry entry, UnitData unitData, ref UnitAnimationProfileData profile)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Prefab", entry.DisplayName);
            EditorGUILayout.LabelField("Unit Binding", unitData == null ? "(No UnitData)" : $"[{unitData.Id}] {unitData.Name}");
            EditorGUILayout.LabelField("Animation Data", profile == null ? "(None)" : $"[{profile.Id}] {profile.UnitName}");

            if (profile == null)
            {
                if (GUILayout.Button("Create Animation Data", GUILayout.Width(180f)))
                {
                    CreateProfile(entry, unitData);
                    profile = ResolveProfile(entry);
                }

                if (_rows.Count > 0)
                {
                    string[] options = _rows.Select(row => $"[{row.Id}] {row.UnitName}").ToArray();
                    _copySourceProfileIndex = Mathf.Clamp(_copySourceProfileIndex, 0, options.Length - 1);
                    _copySourceProfileIndex = EditorGUILayout.Popup("Copy Source", _copySourceProfileIndex, options);
                    if (GUILayout.Button("Copy Existing Animation Data", GUILayout.Width(220f)))
                    {
                        CreateProfileFromSource(entry, unitData, _rows[_copySourceProfileIndex]);
                        profile = ResolveProfile(entry);
                    }
                }
            }
            else if (GUILayout.Button("Delete Animation Data", GUILayout.Width(180f)))
            {
                _rows.Remove(profile);
                NormalizeRowIds();
                _isDirty = true;
                profile = null;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawUnitInfo(UnitPrefabEntry entry, UnitData unitData, UnitAnimationProfileData profile)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Prefab Path", entry.AssetPath ?? string.Empty);
                EditorGUILayout.IntField("Unit Data Id", unitData?.Id ?? -1);
                EditorGUILayout.TextField("Unit Name", unitData?.Name ?? entry.DisplayName);
            }

            profile.UnitDataId = unitData?.Id ?? -1;
            profile.UnitName = unitData?.Name ?? entry.DisplayName;
        }

        private void DrawAnimationEntry(
            UnitData unitData,
            UnitPrefabEntry entry,
            UnitAnimationProfileData profile,
            int index,
            UnitAnimationEntryData animation)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Animation {index}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(70f)))
            {
                profile.Animations.RemoveAt(index);
                _isDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            int stateIndex = Array.IndexOf(KnownStateNames, animation.StateName);
            if (stateIndex >= 0)
            {
                int nextStateIndex = EditorGUILayout.Popup("State", stateIndex, KnownStateNames);
                animation.StateName = KnownStateNames[nextStateIndex];
            }
            else
            {
                animation.StateName = EditorGUILayout.TextField("State", animation.StateName);
            }

            animation.AnimationName = EditorGUILayout.TextField("Animation Name", animation.AnimationName ?? string.Empty);
            DrawSkillNameHelper(unitData, ref animation.AnimationName);

            UnitSpriteAnimationClip currentClip = string.IsNullOrWhiteSpace(animation.SpriteClipPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<UnitSpriteAnimationClip>(animation.SpriteClipPath);
            UnitSpriteAnimationClip nextClip = (UnitSpriteAnimationClip)EditorGUILayout.ObjectField(
                "Sprite Sequence Clip",
                currentClip,
                typeof(UnitSpriteAnimationClip),
                false);
            animation.SpriteClipPath = nextClip != null ? AssetDatabase.GetAssetPath(nextClip) : string.Empty;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Sprite Clip", GUILayout.Width(150f)))
            {
                UnitSpriteAnimationClip createdClip = CreateSpriteClip(entry, animation);
                animation.SpriteClipPath = AssetDatabase.GetAssetPath(createdClip);
                Selection.activeObject = createdClip;
                EditorGUIUtility.PingObject(createdClip);
                _isDirty = true;
            }

            using (new EditorGUI.DisabledScope(nextClip == null))
            {
                if (GUILayout.Button("Open Sprite Clip", GUILayout.Width(150f)))
                {
                    Selection.activeObject = nextClip;
                    EditorGUIUtility.PingObject(nextClip);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Configure FPS, looping, reference frame size, and Front/Back/Left Sprite arrays on the Sprite Sequence Clip. Right automatically mirrors Left. Every frame uses its own Sprite rect and pivot.",
                MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private static UnitSpriteAnimationClip CreateSpriteClip(UnitPrefabEntry entry, UnitAnimationEntryData animation)
        {
            EnsureAssetFolder(SpriteClipDirectory);
            string stateName = string.IsNullOrWhiteSpace(animation.StateName) ? "State" : animation.StateName;
            string animationName = string.IsNullOrWhiteSpace(animation.AnimationName) ? "Default" : animation.AnimationName;
            string fileName = SanitizeFileName($"{entry.DisplayName}_{stateName}_{animationName}");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{SpriteClipDirectory}/{fileName}.asset");
            UnitSpriteAnimationClip clip = ScriptableObject.CreateInstance<UnitSpriteAnimationClip>();
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            return clip;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
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
            return string.IsNullOrWhiteSpace(value) ? "UnitSpriteAnimationClip" : value;
        }

        private void DrawSkillNameHelper(UnitData unitData, ref string animationName)
        {
            List<string> skillNames = CollectSkillDisplayNames(unitData);
            if (skillNames.Count == 0)
                return;

            string[] options = new string[skillNames.Count + 1];
            options[0] = "(No Skill Name)";
            for (int i = 0; i < skillNames.Count; i++)
                options[i + 1] = skillNames[i];

            int selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(animationName))
            {
                for (int i = 0; i < skillNames.Count; i++)
                {
                    if (!string.Equals(skillNames[i], animationName, StringComparison.Ordinal))
                        continue;

                    selectedIndex = i + 1;
                    break;
                }
            }

            int nextIndex = EditorGUILayout.Popup("Skill Name Helper", selectedIndex, options);
            animationName = nextIndex <= 0 ? animationName : skillNames[nextIndex - 1];
        }

        private List<string> CollectSkillDisplayNames(UnitData unitData)
        {
            List<string> names = new();
            UnitSkillModuleData skillModule = unitData?.GetModule<UnitSkillModuleData>();
            if (skillModule?.Skills == null)
                return names;

            for (int i = 0; i < skillModule.Skills.Count; i++)
            {
                UnitSkillSlotData slot = skillModule.Skills[i];
                if (slot == null || slot.SkillId < 0)
                    continue;

                SkillData skillData = EditorComponents.Data.Get<SkillData>(slot.SkillId);
                string name = skillData?.DisplayName;
                if (string.IsNullOrWhiteSpace(name) || names.Contains(name))
                    continue;

                names.Add(name);
            }

            return names;
        }

        private void CreateProfile(UnitPrefabEntry entry, UnitData unitData)
        {
            UnitAnimationProfileData profile = new()
            {
                Id = _rows.Count + 1,
                UnitDataId = unitData?.Id ?? -1,
                UnitName = unitData?.Name ?? entry.DisplayName,
                PlaybackSpeed = 1f,
            };
            profile.Normalize();
            _rows.Add(profile);
            NormalizeRowIds();
            _isDirty = true;
        }

        private void CreateProfileFromSource(UnitPrefabEntry entry, UnitData unitData, UnitAnimationProfileData source)
        {
            if (source == null)
            {
                CreateProfile(entry, unitData);
                return;
            }

            string json = JsonConvert.SerializeObject(source, JsonSettings);
            UnitAnimationProfileData copy = JsonConvert.DeserializeObject<UnitAnimationProfileData>(json, JsonSettings);
            if (copy == null)
            {
                CreateProfile(entry, unitData);
                return;
            }

            copy.UnitDataId = unitData?.Id ?? -1;
            copy.UnitName = unitData?.Name ?? entry.DisplayName;
            copy.Normalize();
            _rows.Add(copy);
            NormalizeRowIds();
            _isDirty = true;
        }

        private void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 0f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        }

        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height + 1f;
            rect.height = 1f;
            EditorGUI.DrawRect(rect, new Color(0.45f, 0.45f, 0.45f));
            GUILayout.Space(4f);
        }

        private void LoadAll()
        {
            LoadProfiles();
            LoadUnits();
            RefreshPrefabEntries();
            if (_selectedIndex < 0 && _prefabEntries.Count > 0)
                _selectedIndex = 0;
        }

        private void LoadProfiles()
        {
            _rows.Clear();
            _selectedIndex = -1;
            _isDirty = false;

            if (!File.Exists(ProfileDataPath))
            {
                _statusText = $"Missing file: {ProfileDataPath}";
                return;
            }

            try
            {
                ProfileTableWrapper wrapper = JsonConvert.DeserializeObject<ProfileTableWrapper>(DataFileUtility.ReadJsonText(ProfileDataPath), JsonSettings);
                if (wrapper?.Rows != null)
                    _rows.AddRange(wrapper.Rows);

                NormalizeRowIds();
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                Debug.LogError($"[UnitAnimationEditor] Load profile error:\n{ex}");
            }
        }

        private void LoadUnits()
        {
            _units.Clear();
            if (!File.Exists(UnitDataPath))
                return;

            try
            {
                UnitTableWrapper wrapper = JsonConvert.DeserializeObject<UnitTableWrapper>(DataFileUtility.ReadJsonText(UnitDataPath), JsonSettings);
                if (wrapper?.Rows != null)
                    _units.AddRange(wrapper.Rows);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnitAnimationEditor] Load unit error:\n{ex}");
            }
        }

        private void SaveData()
        {
            string directory = Path.GetDirectoryName(ProfileDataPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                NormalizeRowIds();
                foreach (UnitAnimationProfileData row in _rows)
                    row?.Normalize();

                string json = JsonConvert.SerializeObject(new ProfileTableWrapper { Rows = _rows }, JsonSettings);
                DataFileUtility.WriteJsonText(ProfileDataPath, json);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved {_rows.Count} unit animation profiles";
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                Debug.LogError($"[UnitAnimationEditor] Save error:\n{ex}");
            }
        }

        private void NormalizeRowIds()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i] ??= new UnitAnimationProfileData();
                _rows[i].Id = i + 1;
                _rows[i].Normalize();
            }
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
                    continue;

                _prefabEntries.Add(new UnitPrefabEntry
                {
                    AssetPath = path,
                    Prefab = prefab,
                });
            }

            _prefabEntries.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            _selectedIndex = _prefabEntries.Count == 0 ? -1 : Mathf.Clamp(_selectedIndex, 0, _prefabEntries.Count - 1);
            _statusText = $"Loaded {_prefabEntries.Count} prefabs, {_rows.Count} animation profiles";
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
                return null;

            UnitData byPath = _units.FirstOrDefault(row => string.Equals(row.PrefabPath, entry.AssetPath, StringComparison.Ordinal));
            if (byPath != null)
                return byPath;

            return _units.FirstOrDefault(row => string.Equals(row.Name, entry.DisplayName, StringComparison.Ordinal));
        }

        private UnitAnimationProfileData ResolveProfile(UnitPrefabEntry entry)
        {
            if (entry == null)
                return null;

            UnitData unitData = ResolveUnitData(entry);
            if (unitData != null)
            {
                UnitAnimationProfileData byUnitId = _rows.FirstOrDefault(row => row != null && row.UnitDataId == unitData.Id);
                if (byUnitId != null)
                    return byUnitId;
            }

            return _rows.FirstOrDefault(row =>
                row != null &&
                !string.IsNullOrWhiteSpace(row.UnitName) &&
                string.Equals(row.UnitName, entry.DisplayName, StringComparison.Ordinal));
        }
    }
}
