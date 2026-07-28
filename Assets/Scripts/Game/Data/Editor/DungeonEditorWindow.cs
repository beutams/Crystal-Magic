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
    public partial class DungeonEditorWindow : EditorWindow
    {
        private const string ThemeDataPath = "Assets/Res/Data/DungeonThemeDataTable.json";
        private const string MonsterPoolDataPath = "Assets/Res/Data/DungeonMonsterPoolDataTable.json";
        private const string TreasurePoolDataPath = "Assets/Res/Data/DungeonTreasurePoolDataTable.json";
        private const string BossRoomDataPath = "Assets/Res/Data/DungeonBossRoomDataTable.json";
        private const float ListPanelWidth = 240f;

        private enum DungeonTab
        {
            Theme,
            MonsterPool,
            TreasurePool,
            BossRoom,
        }

        private enum ThemeSubTab
        {
            Overview,
            VisualStyles,
            Style,
            WallTiles,
            DoorTiles,
            RoomProfiles,
            AnteRoomProfiles,
        }

        private sealed class ThemeTableWrapper
        {
            public List<DungeonThemeData> Rows = new();
        }

        private sealed class MonsterPoolTableWrapper
        {
            public List<DungeonMonsterPoolData> Rows = new();
        }

        private sealed class BossRoomTableWrapper
        {
            public List<DungeonBossRoomData> Rows = new();
        }

        private sealed class TreasurePoolTableWrapper
        {
            public List<DungeonTreasurePoolData> Rows = new();
        }

        private sealed class IntOption
        {
            public int Id;
            public string Label;
        }

        private sealed class StringOption
        {
            public string Value;
            public string Label;
        }

        private static JsonSerializerSettings JsonSettings => new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly List<DungeonThemeData> _themes = new();
        private readonly List<DungeonMonsterPoolData> _monsterPools = new();
        private readonly List<DungeonTreasurePoolData> _treasurePools = new();
        private readonly List<DungeonBossRoomData> _bossRooms = new();
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly List<string> _themeStyleValidationMessages = new();

        private DungeonTab _selectedTab;
        private ThemeSubTab _selectedThemeSubTab;
        private int _selectedThemeIndex = -1;
        private int _selectedStyleIndex = -1;
        private int _selectedRoomProfileIndex = -1;
        private int _selectedAnteProfileIndex = -1;
        private Vector2 _listScrollPos;
        private Vector2 _detailScrollPos;
        private bool _isDirty;
        private string _statusText = string.Empty;

        [MenuItem("Tools/Data/Dungeon Editor")]
        public static void Open()
        {
            DungeonEditorWindow window = GetWindow<DungeonEditorWindow>("Dungeon Editor");
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
            DrawTabs();

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
                LoadAll();

            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "Save *" : "Save", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                SaveAll();
            GUI.enabled = true;

            GUI.enabled = _selectedTab == DungeonTab.Theme;
            if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                AddRowForSelectedTab();
            GUI.enabled = true;

            if (_selectedTab == DungeonTab.Theme
                && GUILayout.Button("Validate Styles", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                ValidateThemeStyles();

            if (_selectedTab == DungeonTab.Theme && HasSelection())
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    DuplicateSelectedRow();
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    DeleteSelectedRow();
            }

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_statusText))
                GUILayout.Label(_statusText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(_selectedTab == DungeonTab.Theme, "Themes", EditorStyles.toolbarButton))
                SelectTab(DungeonTab.Theme);
            if (GUILayout.Toggle(_selectedTab == DungeonTab.MonsterPool, "Monster Pools", EditorStyles.toolbarButton))
                SelectTab(DungeonTab.MonsterPool);
            if (GUILayout.Toggle(_selectedTab == DungeonTab.TreasurePool, "Treasure Pools", EditorStyles.toolbarButton))
                SelectTab(DungeonTab.TreasurePool);
            if (GUILayout.Toggle(_selectedTab == DungeonTab.BossRoom, "Boss Rooms", EditorStyles.toolbarButton))
                SelectTab(DungeonTab.BossRoom);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListPanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(GetListTitle(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            switch (_selectedTab)
            {
                case DungeonTab.Theme:
                case DungeonTab.MonsterPool:
                case DungeonTab.TreasurePool:
                case DungeonTab.BossRoom:
                    DrawThemeList();
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawThemeList()
        {
            for (int i = 0; i < _themes.Count; i++)
            {
                DungeonThemeData row = _themes[i];
                string label = $"[{row.Id}] {row.Name}";
                if (GUILayout.Toggle(i == _selectedThemeIndex, label, "Button")
                    && _selectedThemeIndex != i)
                {
                    CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
                    _selectedThemeIndex = i;
                    _selectedThemeSubTab = ThemeSubTab.Overview;
                    _selectedStyleIndex = -1;
                    _selectedRoomProfileIndex = -1;
                    _selectedAnteProfileIndex = -1;
                }
            }
        }

        private static void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

            switch (_selectedTab)
            {
                case DungeonTab.Theme:
                    DrawThemeDetailPanel();
                    break;
                case DungeonTab.MonsterPool:
                    DrawMonsterPoolDetailPanel();
                    break;
                case DungeonTab.TreasurePool:
                    DrawTreasurePoolDetailPanel();
                    break;
                case DungeonTab.BossRoom:
                    DrawBossRoomDetailPanel();
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawThemeDetailPanel()
        {
            DungeonThemeData row = GetSelectedTheme();
            if (row == null)
            {
                EditorGUILayout.HelpBox("Select one dungeon theme.", MessageType.Info);
                return;
            }

            row.EnsureValid();

            DrawThemeSubTabs();
            GUILayout.Space(8f);

            switch (_selectedThemeSubTab)
            {
                case ThemeSubTab.Overview:
                    DrawThemeOverview(row);
                    break;
                case ThemeSubTab.VisualStyles:
                    DrawThemeVisualStyles(row);
                    break;
                case ThemeSubTab.Style:
                    DrawStyleSettings(row, GetSelectedStyle(row));
                    break;
                case ThemeSubTab.WallTiles:
                    DrawWallTileSettings(GetSelectedStyle(row));
                    break;
                case ThemeSubTab.DoorTiles:
                    DrawDoorTileSettings(GetSelectedStyle(row));
                    break;
                case ThemeSubTab.RoomProfiles:
                    DrawProfileSettings(GetSelectedStyle(row), false);
                    break;
                case ThemeSubTab.AnteRoomProfiles:
                    DrawProfileSettings(GetSelectedStyle(row), true);
                    break;
            }

            DrawThemeStyleValidationMessages();
        }

        private void DrawThemeSubTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawThemeSubTabToggle(ThemeSubTab.Overview, "Overview");
            DrawThemeSubTabToggle(ThemeSubTab.VisualStyles, "Visual Styles");
            DrawThemeSubTabToggle(ThemeSubTab.Style, "Style");
            DrawThemeSubTabToggle(ThemeSubTab.WallTiles, "Wall Tiles");
            DrawThemeSubTabToggle(ThemeSubTab.DoorTiles, "Door Tiles");
            DrawThemeSubTabToggle(ThemeSubTab.RoomProfiles, "Room Profiles");
            DrawThemeSubTabToggle(ThemeSubTab.AnteRoomProfiles, "Ante Profiles");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawThemeSubTabToggle(ThemeSubTab tab, string label)
        {
            if (GUILayout.Toggle(_selectedThemeSubTab == tab, label, EditorStyles.toolbarButton))
                _selectedThemeSubTab = tab;
        }

        private void DrawThemeOverview(DungeonThemeData row)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Basic", EditorStyles.boldLabel);
            row.Id = EditorGUILayout.IntField("Id", row.Id);
            row.Name = EditorGUILayout.TextField("Name", row.Name ?? string.Empty);
            row.ThemeKey = EditorGUILayout.TextField("Theme Key", row.ThemeKey ?? string.Empty);
            row.FloorStart = Mathf.Max(1, EditorGUILayout.IntField("Floor Start", row.FloorStart));
            row.FloorEnd = Mathf.Max(row.FloorStart, EditorGUILayout.IntField("Floor End", row.FloorEnd));

            GUILayout.Space(8f);
            EditorGUILayout.HelpBox("Use the Visual Styles subpage to configure this theme's styles. Monster pools, treasure pools, and boss rooms remain in the other main tabs.", MessageType.None);

            if (EditorGUI.EndChangeCheck())
            {
                row.EnsureValid();
                _isDirty = true;
            }
        }

        private void DrawMonsterPoolDetailPanel()
        {
            DungeonThemeData theme = GetSelectedTheme();
            if (theme == null)
            {
                EditorGUILayout.HelpBox("Select one dungeon theme.", MessageType.Info);
                return;
            }

            List<StringOption> unitOptions = BuildUnitOptions();
            EditorGUILayout.LabelField($"{theme.Name} Monster Pools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Theme Key: {theme.ThemeKey}", EditorStyles.miniLabel);
            GUILayout.Space(8f);

            DrawMonsterPoolEditor(
                GetOrCreateMonsterPoolForTheme(theme, 1),
                GetSectionFoldoutKey(theme, "monster", 1),
                "Level 1 Pool",
                unitOptions);
            GUILayout.Space(8f);
            DrawMonsterPoolEditor(
                GetOrCreateMonsterPoolForTheme(theme, 2),
                GetSectionFoldoutKey(theme, "monster", 2),
                "Level 2 Pool",
                unitOptions);
            GUILayout.Space(8f);
            DrawMonsterPoolEditor(
                GetOrCreateMonsterPoolForTheme(theme, 3),
                GetSectionFoldoutKey(theme, "monster", 3),
                "Level 3 Pool",
                unitOptions);
        }

        private void DrawTreasurePoolDetailPanel()
        {
            DungeonThemeData theme = GetSelectedTheme();
            if (theme == null)
            {
                EditorGUILayout.HelpBox("Select one dungeon theme.", MessageType.Info);
                return;
            }

            List<IntOption> itemOptions = BuildItemOptions();
            EditorGUILayout.LabelField($"{theme.Name} Treasure Pools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Theme Key: {theme.ThemeKey}", EditorStyles.miniLabel);
            GUILayout.Space(8f);

            DrawTreasurePoolEditor(
                GetOrCreateTreasurePoolForTheme(theme, 1),
                GetSectionFoldoutKey(theme, "treasure", 1),
                "Level 1 Pool",
                itemOptions);
            GUILayout.Space(8f);
            DrawTreasurePoolEditor(
                GetOrCreateTreasurePoolForTheme(theme, 2),
                GetSectionFoldoutKey(theme, "treasure", 2),
                "Level 2 Pool",
                itemOptions);
            GUILayout.Space(8f);
            DrawTreasurePoolEditor(
                GetOrCreateTreasurePoolForTheme(theme, 3),
                GetSectionFoldoutKey(theme, "treasure", 3),
                "Level 3 Pool",
                itemOptions);
        }

        private void DrawBossRoomDetailPanel()
        {
            DungeonThemeData theme = GetSelectedTheme();
            if (theme == null)
            {
                EditorGUILayout.HelpBox("Select one dungeon theme.", MessageType.Info);
                return;
            }

            List<StringOption> themeKeyOptions = BuildThemeKeyOptions();
            List<IntOption> poolOptions = BuildMonsterPoolOptions();
            List<IntOption> treasurePoolOptions = BuildTreasurePoolOptions();
            EditorGUILayout.LabelField($"{theme.Name} Boss Rooms", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Theme Key: {theme.ThemeKey}", EditorStyles.miniLabel);
            GUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Boss Room Variants", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Boss Room", GUILayout.Width(108f)))
                AddBossRoomForTheme(theme);
            EditorGUILayout.EndHorizontal();

            theme.BossRoomIds ??= new List<int>();
            if (theme.BossRoomIds.Count == 0)
            {
                EditorGUILayout.HelpBox("This theme does not have any boss room variants yet.", MessageType.None);
                return;
            }

            for (int i = 0; i < theme.BossRoomIds.Count; i++)
            {
                int bossRoomId = theme.BossRoomIds[i];
                DungeonBossRoomData row = GetBossRoomById(bossRoomId);
                if (row == null)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"Boss Room {i + 1}", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox($"Missing boss room #{bossRoomId}.", MessageType.Warning);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Recreate"))
                    {
                        theme.BossRoomIds[i] = CreateBossRoomForTheme(theme, i + 1).Id;
                        _isDirty = true;
                    }

                    if (GUILayout.Button("Remove Ref"))
                    {
                        theme.BossRoomIds.RemoveAt(i);
                        _isDirty = true;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }

                DrawBossRoomEditor(
                    theme,
                    row,
                    i,
                    GetSectionFoldoutKey(theme, "boss", i + 1),
                    themeKeyOptions,
                    poolOptions,
                    treasurePoolOptions);
                GUILayout.Space(8f);
            }
        }

        private DungeonThemeData GetSelectedTheme()
        {
            if (_selectedThemeIndex < 0 || _selectedThemeIndex >= _themes.Count)
                return null;

            DungeonThemeData theme = _themes[_selectedThemeIndex];
            theme?.EnsureValid();
            return theme;
        }

        private DungeonMonsterPoolData GetOrCreateMonsterPoolForTheme(DungeonThemeData theme, int level)
        {
            int poolId = level switch
            {
                1 => theme.Mob1PoolId,
                2 => theme.Mob2PoolId,
                3 => theme.Mob3PoolId,
                _ => -1,
            };

            DungeonMonsterPoolData pool = GetMonsterPoolById(poolId);
            if (pool != null)
                return pool;

            pool = new DungeonMonsterPoolData
            {
                Id = GetNextId(_monsterPools.Select(static row => row.Id)),
                Name = $"{theme.Name} Mob{level}",
            };
            pool.EnsureValid();
            _monsterPools.Add(pool);
            switch (level)
            {
                case 1:
                    theme.Mob1PoolId = pool.Id;
                    break;
                case 2:
                    theme.Mob2PoolId = pool.Id;
                    break;
                case 3:
                    theme.Mob3PoolId = pool.Id;
                    break;
            }

            _isDirty = true;
            return pool;
        }

        private DungeonTreasurePoolData GetOrCreateTreasurePoolForTheme(DungeonThemeData theme, int level)
        {
            int poolId = level switch
            {
                1 => theme.Treasure1PoolId,
                2 => theme.Treasure2PoolId,
                3 => theme.Treasure3PoolId,
                _ => -1,
            };

            DungeonTreasurePoolData pool = GetTreasurePoolById(poolId);
            if (pool != null)
                return pool;

            pool = new DungeonTreasurePoolData
            {
                Id = GetNextId(_treasurePools.Select(static row => row.Id)),
                Name = $"{theme.Name} Treasure{level}",
            };
            pool.EnsureValid();
            _treasurePools.Add(pool);
            switch (level)
            {
                case 1:
                    theme.Treasure1PoolId = pool.Id;
                    break;
                case 2:
                    theme.Treasure2PoolId = pool.Id;
                    break;
                case 3:
                    theme.Treasure3PoolId = pool.Id;
                    break;
            }

            _isDirty = true;
            return pool;
        }

        private DungeonBossRoomData CreateBossRoomForTheme(DungeonThemeData theme, int displayIndex)
        {
            DungeonBossRoomData room = new()
            {
                Id = GetNextId(_bossRooms.Select(static row => row.Id)),
                Name = $"{theme.Name} Boss Room {displayIndex}",
                ThemeKey = theme.ThemeKey ?? string.Empty,
                RewardTreasurePoolId = GetOrCreateTreasurePoolForTheme(theme, 3).Id,
            };
            room.EnsureValid();
            _bossRooms.Add(room);
            theme.BossRoomIds.Add(room.Id);
            return room;
        }

        private void AddBossRoomForTheme(DungeonThemeData theme)
        {
            int displayIndex = (theme.BossRoomIds?.Count ?? 0) + 1;
            CreateBossRoomForTheme(theme, displayIndex);
            _isDirty = true;
        }

        private DungeonMonsterPoolData GetMonsterPoolById(int id)
        {
            return _monsterPools.FirstOrDefault(row => row != null && row.Id == id);
        }

        private DungeonTreasurePoolData GetTreasurePoolById(int id)
        {
            return _treasurePools.FirstOrDefault(row => row != null && row.Id == id);
        }

        private DungeonBossRoomData GetBossRoomById(int id)
        {
            return _bossRooms.FirstOrDefault(row => row != null && row.Id == id);
        }

        private static string GetSectionFoldoutKey(DungeonThemeData theme, string section, int index)
        {
            return $"{theme?.Id ?? -1}:{section}:{index}";
        }

        private bool DrawSectionFoldout(string key, string title)
        {
            if (!_foldoutStates.TryGetValue(key, out bool expanded))
                expanded = true;

            bool nextExpanded = EditorGUILayout.Foldout(expanded, title, true);
            _foldoutStates[key] = nextExpanded;
            return nextExpanded;
        }

        private void DrawMonsterPoolEditor(DungeonMonsterPoolData row, string foldoutKey, string title, List<StringOption> unitOptions)
        {
            row.EnsureValid();
            EditorGUILayout.BeginVertical("box");
            bool isExpanded = DrawSectionFoldout(foldoutKey, title);
            if (!isExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            row.Name = EditorGUILayout.TextField("Pool Name", row.Name ?? string.Empty);

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Entry", GUILayout.Width(84f)))
                row.Entries.Add(new DungeonMonsterPoolEntryData());
            EditorGUILayout.EndHorizontal();

            row.Entries ??= new List<DungeonMonsterPoolEntryData>();
            for (int i = 0; i < row.Entries.Count; i++)
            {
                DungeonMonsterPoolEntryData entry = row.Entries[i] ?? new DungeonMonsterPoolEntryData();
                row.Entries[i] = entry;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    row.Entries.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                entry.UnitName = DrawStringPopup("Unit", entry.UnitName, unitOptions);
                entry.Weight = Mathf.Max(1, EditorGUILayout.IntField("Probability Weight", entry.Weight));
                entry.MinFloor = Mathf.Max(1, EditorGUILayout.IntField("Min Floor", entry.MinFloor));
                entry.MaxFloor = Mathf.Max(entry.MinFloor, EditorGUILayout.IntField("Max Floor", entry.MaxFloor));
                entry.BossOnly = EditorGUILayout.Toggle("Boss Only", entry.BossOnly);
                EditorGUILayout.EndVertical();
            }

            if (EditorGUI.EndChangeCheck())
            {
                row.EnsureValid();
                _isDirty = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTreasurePoolEditor(DungeonTreasurePoolData row, string foldoutKey, string title, List<IntOption> itemOptions)
        {
            row.EnsureValid();
            EditorGUILayout.BeginVertical("box");
            bool isExpanded = DrawSectionFoldout(foldoutKey, title);
            if (!isExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            row.Name = EditorGUILayout.TextField("Pool Name", row.Name ?? string.Empty);

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Entry", GUILayout.Width(84f)))
                row.Entries.Add(new DungeonTreasurePoolEntryData());
            EditorGUILayout.EndHorizontal();

            row.Entries ??= new List<DungeonTreasurePoolEntryData>();
            for (int i = 0; i < row.Entries.Count; i++)
            {
                DungeonTreasurePoolEntryData entry = row.Entries[i] ?? new DungeonTreasurePoolEntryData();
                row.Entries[i] = entry;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    row.Entries.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                entry.Weight = Mathf.Max(1, EditorGUILayout.IntField("Probability Weight", entry.Weight));
                entry.MinFloor = Mathf.Max(1, EditorGUILayout.IntField("Min Floor", entry.MinFloor));
                entry.MaxFloor = Mathf.Max(entry.MinFloor, EditorGUILayout.IntField("Max Floor", entry.MaxFloor));

                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Rewards", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Add Reward", GUILayout.Width(88f)))
                    entry.Rewards.Add(new DropEntryData());
                EditorGUILayout.EndHorizontal();

                entry.Rewards ??= new List<DropEntryData>();
                for (int rewardIndex = 0; rewardIndex < entry.Rewards.Count; rewardIndex++)
                {
                    DropEntryData reward = entry.Rewards[rewardIndex] ?? new DropEntryData();
                    entry.Rewards[rewardIndex] = reward;

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Reward {rewardIndex + 1}", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                    {
                        entry.Rewards.RemoveAt(rewardIndex);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    reward.DropType = (DropRewardType)EditorGUILayout.EnumPopup("Reward Type", reward.DropType);
                    if (reward.DropType == DropRewardType.Item)
                        reward.ItemId = DrawIntPopup("Item", reward.ItemId, itemOptions);
                    else
                        EditorGUILayout.HelpBox("Money reward does not require an item id.", MessageType.None);
                    reward.Chance = EditorGUILayout.Slider("Chance", reward.Chance, 0f, 1f);
                    reward.MinQuantity = Mathf.Max(0, EditorGUILayout.IntField("Min Quantity", reward.MinQuantity));
                    reward.MaxQuantity = Mathf.Max(reward.MinQuantity, EditorGUILayout.IntField("Max Quantity", reward.MaxQuantity));
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
            }

            if (EditorGUI.EndChangeCheck())
            {
                row.EnsureValid();
                _isDirty = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBossRoomEditor(
            DungeonThemeData theme,
            DungeonBossRoomData row,
            int themeBossRoomIndex,
            string foldoutKey,
            List<StringOption> themeKeyOptions,
            List<IntOption> poolOptions,
            List<IntOption> treasurePoolOptions)
        {
            row.EnsureValid();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            bool isExpanded = DrawSectionFoldout(foldoutKey, $"Boss Room {themeBossRoomIndex + 1}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete Room", GUILayout.Width(92f)))
            {
                theme.BossRoomIds.RemoveAt(themeBossRoomIndex);
                _bossRooms.Remove(row);
                _isDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!isExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();

            row.Name = EditorGUILayout.TextField("Name", row.Name ?? string.Empty);
            row.ThemeKey = DrawStringPopup("Theme Key", row.ThemeKey, themeKeyOptions, allowCustom: true);
            row.FloorBandStart = Mathf.Max(1, EditorGUILayout.IntField("Band Start", row.FloorBandStart));
            row.FloorBandEnd = Mathf.Max(row.FloorBandStart, EditorGUILayout.IntField("Band End", row.FloorBandEnd));
            row.Width = Mathf.Max(8, EditorGUILayout.IntField("Width", row.Width));
            row.Height = Mathf.Max(8, EditorGUILayout.IntField("Height", row.Height));
            row.RewardTreasurePoolId = DrawIntPopup("Reward Treasure Pool", row.RewardTreasurePoolId, treasurePoolOptions);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Anchors", EditorStyles.boldLabel);
            row.PlayerSpawn = DrawInt2("Player Spawn", row.PlayerSpawn);
            row.ExitSpawn = DrawInt2("Exit Spawn", row.ExitSpawn);
            row.RewardSpawn = DrawInt2("Reward Spawn", row.RewardSpawn);
            row.BossSpawn = DrawInt2("Boss Spawn", row.BossSpawn);

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Boss Pools", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Pool", GUILayout.Width(72f)))
                row.BossPoolIds.Add(_monsterPools.Count > 0 ? _monsterPools[0].Id : -1);
            EditorGUILayout.EndHorizontal();

            row.BossPoolIds ??= new List<int>();
            for (int i = 0; i < row.BossPoolIds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                row.BossPoolIds[i] = DrawIntPopup($"Boss Pool {i + 1}", row.BossPoolIds[i], poolOptions);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    row.BossPoolIds.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Support Spawn Points", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Spawn", GUILayout.Width(84f)))
                row.SupportSpawnPoints.Add(new Int2Data(4, 4));
            EditorGUILayout.EndHorizontal();

            row.SupportSpawnPoints ??= new List<Int2Data>();
            for (int i = 0; i < row.SupportSpawnPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                row.SupportSpawnPoints[i] = DrawInt2($"Spawn {i + 1}", row.SupportSpawnPoints[i]);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    row.SupportSpawnPoints.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                row.ThemeKey = string.IsNullOrWhiteSpace(row.ThemeKey) ? theme.ThemeKey : row.ThemeKey;
                row.EnsureValid();
                _isDirty = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void AddRowForSelectedTab()
        {
            if (_selectedTab != DungeonTab.Theme)
                return;

            _themes.Add(new DungeonThemeData
            {
                Id = GetNextId(_themes.Select(static row => row.Id)),
                Name = $"Theme {_themes.Count + 1}",
                ThemeKey = $"theme_{_themes.Count + 1:D2}",
                FloorStart = 1,
                FloorEnd = 10,
            });
            _selectedThemeIndex = _themes.Count - 1;
            _selectedThemeSubTab = ThemeSubTab.Overview;
            _selectedStyleIndex = -1;
            _selectedRoomProfileIndex = -1;
            _selectedAnteProfileIndex = -1;

            _isDirty = true;
        }

        private void DuplicateSelectedRow()
        {
            if (_selectedTab == DungeonTab.Theme)
                DuplicateTheme();
        }

        private void DeleteSelectedRow()
        {
            if (_selectedTab != DungeonTab.Theme)
                return;

            if (_selectedThemeIndex >= 0 && _selectedThemeIndex < _themes.Count)
            {
                _themes.RemoveAt(_selectedThemeIndex);
                _selectedThemeIndex = Mathf.Clamp(_selectedThemeIndex, -1, _themes.Count - 1);
                _selectedThemeSubTab = ThemeSubTab.Overview;
                _selectedStyleIndex = -1;
                _selectedRoomProfileIndex = -1;
                _selectedAnteProfileIndex = -1;
                _isDirty = true;
            }
        }

        private void DuplicateTheme()
        {
            if (_selectedThemeIndex < 0 || _selectedThemeIndex >= _themes.Count)
                return;

            DungeonThemeData source = _themes[_selectedThemeIndex];
            DungeonThemeData copy = DeepCopy(source);
            copy.Id = GetNextId(_themes.Select(static row => row.Id));
            copy.Name = $"{copy.Name} Copy";
            copy.Mob1PoolId = DuplicateMonsterPoolForTheme(source.Mob1PoolId, $"{copy.Name} Mob1");
            copy.Mob2PoolId = DuplicateMonsterPoolForTheme(source.Mob2PoolId, $"{copy.Name} Mob2");
            copy.Mob3PoolId = DuplicateMonsterPoolForTheme(source.Mob3PoolId, $"{copy.Name} Mob3");
            copy.Treasure1PoolId = DuplicateTreasurePoolForTheme(source.Treasure1PoolId, $"{copy.Name} Treasure1");
            copy.Treasure2PoolId = DuplicateTreasurePoolForTheme(source.Treasure2PoolId, $"{copy.Name} Treasure2");
            copy.Treasure3PoolId = DuplicateTreasurePoolForTheme(source.Treasure3PoolId, $"{copy.Name} Treasure3");
            copy.BossRoomIds = DuplicateBossRoomsForTheme(source.BossRoomIds, copy);
            copy.EnsureValid();
            _themes.Add(copy);
            _selectedThemeIndex = _themes.Count - 1;
            _selectedThemeSubTab = ThemeSubTab.Overview;
            _selectedStyleIndex = -1;
            _selectedRoomProfileIndex = -1;
            _selectedAnteProfileIndex = -1;
            _isDirty = true;
        }

        private int DuplicateMonsterPoolForTheme(int sourcePoolId, string nameOverride)
        {
            DungeonMonsterPoolData sourcePool = GetMonsterPoolById(sourcePoolId);
            if (sourcePool == null)
                return -1;

            DungeonMonsterPoolData copy = DeepCopy(sourcePool);
            copy.Id = GetNextId(_monsterPools.Select(static row => row.Id));
            copy.Name = nameOverride;
            copy.EnsureValid();
            _monsterPools.Add(copy);
            return copy.Id;
        }

        private int DuplicateTreasurePoolForTheme(int sourcePoolId, string nameOverride)
        {
            DungeonTreasurePoolData sourcePool = GetTreasurePoolById(sourcePoolId);
            if (sourcePool == null)
                return -1;

            DungeonTreasurePoolData copy = DeepCopy(sourcePool);
            copy.Id = GetNextId(_treasurePools.Select(static row => row.Id));
            copy.Name = nameOverride;
            copy.EnsureValid();
            _treasurePools.Add(copy);
            return copy.Id;
        }

        private List<int> DuplicateBossRoomsForTheme(List<int> sourceBossRoomIds, DungeonThemeData duplicatedTheme)
        {
            List<int> duplicatedIds = new();
            if (sourceBossRoomIds == null)
                return duplicatedIds;

            for (int i = 0; i < sourceBossRoomIds.Count; i++)
            {
                DungeonBossRoomData sourceRoom = GetBossRoomById(sourceBossRoomIds[i]);
                if (sourceRoom == null)
                    continue;

                DungeonBossRoomData copy = DeepCopy(sourceRoom);
                copy.Id = GetNextId(_bossRooms.Select(static row => row.Id));
                copy.Name = $"{duplicatedTheme.Name} Boss Room {i + 1}";
                copy.ThemeKey = duplicatedTheme.ThemeKey ?? string.Empty;
                copy.EnsureValid();
                _bossRooms.Add(copy);
                duplicatedIds.Add(copy.Id);
            }

            return duplicatedIds;
        }

        private void LoadAll()
        {
            _themes.Clear();
            _monsterPools.Clear();
            _treasurePools.Clear();
            _bossRooms.Clear();
            _selectedThemeIndex = -1;
            _selectedThemeSubTab = ThemeSubTab.Overview;
            _selectedStyleIndex = -1;
            _selectedRoomProfileIndex = -1;
            _selectedAnteProfileIndex = -1;
            _themeStyleValidationMessages.Clear();
            _isDirty = false;

            try
            {
                ThemeTableWrapper themeWrapper = LoadWrapper<ThemeTableWrapper>(ThemeDataPath);
                MonsterPoolTableWrapper poolWrapper = LoadWrapper<MonsterPoolTableWrapper>(MonsterPoolDataPath);
                TreasurePoolTableWrapper treasureWrapper = LoadWrapper<TreasurePoolTableWrapper>(TreasurePoolDataPath);
                BossRoomTableWrapper bossWrapper = LoadWrapper<BossRoomTableWrapper>(BossRoomDataPath);

                if (themeWrapper?.Rows != null)
                {
                    _themes.AddRange(themeWrapper.Rows);
                    for (int i = 0; i < _themes.Count; i++)
                        _themes[i]?.EnsureValid();
                }

                if (poolWrapper?.Rows != null)
                {
                    _monsterPools.AddRange(poolWrapper.Rows);
                    for (int i = 0; i < _monsterPools.Count; i++)
                        _monsterPools[i]?.EnsureValid();
                }

                if (treasureWrapper?.Rows != null)
                {
                    _treasurePools.AddRange(treasureWrapper.Rows);
                    for (int i = 0; i < _treasurePools.Count; i++)
                        _treasurePools[i]?.EnsureValid();
                }

                if (bossWrapper?.Rows != null)
                {
                    _bossRooms.AddRange(bossWrapper.Rows);
                    for (int i = 0; i < _bossRooms.Count; i++)
                        _bossRooms[i]?.EnsureValid();
                }

                _statusText = $"Loaded Themes:{_themes.Count} MonsterPools:{_monsterPools.Count} TreasurePools:{_treasurePools.Count} BossRooms:{_bossRooms.Count}";
            }
            catch (Exception ex)
            {
                _statusText = $"Load failed: {ex.Message}";
                Debug.LogError($"[DungeonEditor] Load error:\n{ex}");
            }
        }

        private void SaveAll()
        {
            try
            {
                for (int i = 0; i < _themes.Count; i++)
                    _themes[i]?.EnsureValid();
                for (int i = 0; i < _monsterPools.Count; i++)
                    _monsterPools[i]?.EnsureValid();
                for (int i = 0; i < _treasurePools.Count; i++)
                    _treasurePools[i]?.EnsureValid();
                for (int i = 0; i < _bossRooms.Count; i++)
                    _bossRooms[i]?.EnsureValid();

                SaveWrapper(ThemeDataPath, new ThemeTableWrapper { Rows = _themes });
                SaveWrapper(MonsterPoolDataPath, new MonsterPoolTableWrapper { Rows = _monsterPools });
                SaveWrapper(TreasurePoolDataPath, new TreasurePoolTableWrapper { Rows = _treasurePools });
                SaveWrapper(BossRoomDataPath, new BossRoomTableWrapper { Rows = _bossRooms });

                AssetDatabase.Refresh();
                _isDirty = false;
                _statusText = $"Saved Themes:{_themes.Count} MonsterPools:{_monsterPools.Count} TreasurePools:{_treasurePools.Count} BossRooms:{_bossRooms.Count}";
            }
            catch (Exception ex)
            {
                _statusText = $"Save failed: {ex.Message}";
                Debug.LogError($"[DungeonEditor] Save error:\n{ex}");
            }
        }

        private static T LoadWrapper<T>(string dataPath) where T : class, new()
        {
            if (!File.Exists(dataPath))
                return new T();

            string json = DataFileUtility.ReadJsonText(dataPath);
            return JsonConvert.DeserializeObject<T>(json, JsonSettings) ?? new T();
        }

        private static void SaveWrapper<T>(string dataPath, T wrapper)
        {
            string directory = Path.GetDirectoryName(dataPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(wrapper, JsonSettings);
            DataFileUtility.WriteJsonText(dataPath, json);
        }

        private static T DeepCopy<T>(T source)
        {
            string json = JsonConvert.SerializeObject(source, JsonSettings);
            return JsonConvert.DeserializeObject<T>(json, JsonSettings);
        }

        private string GetListTitle()
        {
            return _selectedTab switch
            {
                DungeonTab.Theme => $"Themes ({_themes.Count})",
                DungeonTab.MonsterPool => $"Themes / Monster Pools ({_themes.Count})",
                DungeonTab.TreasurePool => $"Themes / Treasure Pools ({_themes.Count})",
                DungeonTab.BossRoom => $"Themes / Boss Rooms ({_themes.Count})",
                _ => string.Empty,
            };
        }

        private bool HasSelection()
        {
            return _selectedThemeIndex >= 0 && _selectedThemeIndex < _themes.Count;
        }

        private void SelectTab(DungeonTab tab)
        {
            if (_selectedTab == tab)
                return;

            _selectedTab = tab;
            _listScrollPos = Vector2.zero;
            _detailScrollPos = Vector2.zero;
            CrystalMagic.Editor.EditorFocusUtility.ClearTextFocus();
        }

        private List<IntOption> BuildMonsterPoolOptions()
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
            };

            for (int i = 0; i < _monsterPools.Count; i++)
            {
                DungeonMonsterPoolData row = _monsterPools[i];
                options.Add(new IntOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private List<IntOption> BuildBossRoomOptions()
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
            };

            for (int i = 0; i < _bossRooms.Count; i++)
            {
                DungeonBossRoomData row = _bossRooms[i];
                options.Add(new IntOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private List<IntOption> BuildTreasurePoolOptions()
        {
            List<IntOption> options = new()
            {
                new IntOption { Id = -1, Label = "None" }
            };

            for (int i = 0; i < _treasurePools.Count; i++)
            {
                DungeonTreasurePoolData row = _treasurePools[i];
                options.Add(new IntOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
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

        private static List<StringOption> BuildUnitOptions()
        {
            List<StringOption> options = new()
            {
                new StringOption { Value = string.Empty, Label = "None" }
            };

            foreach (UnitData row in EditorComponents.Data.FindAll<UnitData>(static _ => true).OrderBy(static row => row.Id))
            {
                options.Add(new StringOption
                {
                    Value = row.Name ?? string.Empty,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private List<StringOption> BuildThemeKeyOptions()
        {
            List<StringOption> options = new()
            {
                new StringOption { Value = string.Empty, Label = "None" }
            };

            foreach (string themeKey in _themes
                         .Select(static row => row.ThemeKey ?? string.Empty)
                         .Where(static key => !string.IsNullOrWhiteSpace(key))
                         .Distinct()
                         .OrderBy(static key => key))
            {
                options.Add(new StringOption
                {
                    Value = themeKey,
                    Label = themeKey,
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

        private static string DrawStringPopup(string label, string currentValue, List<StringOption> options, bool allowCustom = false)
        {
            int selectedIndex = 0;
            bool found = false;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Value, currentValue, StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    found = true;
                    break;
                }
            }

            if (allowCustom)
            {
                List<StringOption> localOptions = options;
                if (!found && !string.IsNullOrWhiteSpace(currentValue))
                {
                    localOptions = new List<StringOption>(options)
                    {
                        new StringOption
                        {
                            Value = currentValue,
                            Label = $"{currentValue} (Custom)",
                        }
                    };
                    selectedIndex = localOptions.Count - 1;
                }

                EditorGUILayout.BeginHorizontal();
                int newIndex = EditorGUILayout.Popup(label, selectedIndex, localOptions.Select(static option => option.Label).ToArray());
                string newValue = newIndex >= 0 && newIndex < localOptions.Count ? localOptions[newIndex].Value : currentValue;
                newValue = EditorGUILayout.TextField(newValue ?? string.Empty);
                EditorGUILayout.EndHorizontal();
                return newValue;
            }

            int selected = EditorGUILayout.Popup(label, selectedIndex, options.Select(static option => option.Label).ToArray());
            return selected >= 0 && selected < options.Count ? options[selected].Value : currentValue;
        }

        private static Int2Data DrawInt2(string label, Int2Data value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            value.X = EditorGUILayout.IntField(value.X);
            value.Y = EditorGUILayout.IntField(value.Y);
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private static int GetNextId(IEnumerable<int> ids)
        {
            int nextId = 0;
            foreach (int id in ids)
                nextId = Mathf.Max(nextId, id + 1);
            return nextId;
        }
    }
}
