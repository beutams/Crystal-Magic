// Theme style subpages for DungeonEditorWindow.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public partial class DungeonEditorWindow
    {
        private const string EnvironmentPrefabFolder = "Assets/Res/Prefab/Environment";

        private void DrawThemeVisualStyles(DungeonThemeData theme)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Visual Styles", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Style", GUILayout.Width(84f)))
            {
                DungeonVisualStyleData style = CreateStyle(theme);
                _selectedStyleIndex = theme.VisualStyles.IndexOf(style);
                _selectedThemeSubTab = ThemeSubTab.Style;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("These styles belong only to the selected theme. Select one to edit its region variants, tiles, and interior profiles.", MessageType.None);

            List<IntOption> styleOptions = BuildStyleOptions(theme);
            theme.RootVisualStyleId = DrawIntPopup("Root Style", theme.RootVisualStyleId, styleOptions);
            for (int i = 0; i < theme.VisualStyles.Count; i++)
            {
                DungeonVisualStyleData style = theme.VisualStyles[i] ?? new DungeonVisualStyleData();
                theme.VisualStyles[i] = style;
                EditorGUILayout.BeginHorizontal("box");
                if (GUILayout.Toggle(i == _selectedStyleIndex, $"[{style.Id}] {style.Name}", "Button"))
                {
                    _selectedStyleIndex = i;
                    _selectedThemeSubTab = ThemeSubTab.Style;
                }

                if (GUILayout.Button("Copy", GUILayout.Width(52f)))
                {
                    DungeonVisualStyleData copy = DeepCopy(style);
                    copy.Id = GetNextStyleId(theme);
                    copy.Name = $"{copy.Name} Copy";
                    theme.VisualStyles.Add(copy);
                    _selectedStyleIndex = theme.VisualStyles.Count - 1;
                    _isDirty = true;
                    break;
                }

                GUI.enabled = theme.VisualStyles.Count > 1;
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    int removedId = style.Id;
                    theme.VisualStyles.RemoveAt(i);
                    if (theme.RootVisualStyleId == removedId)
                        theme.RootVisualStyleId = theme.VisualStyles[0].Id;
                    _selectedStyleIndex = Mathf.Clamp(_selectedStyleIndex, -1, theme.VisualStyles.Count - 1);
                    _isDirty = true;
                    GUI.enabled = true;
                    break;
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                theme.EnsureValid();
                _isDirty = true;
            }
        }

        private void DrawStyleSettings(DungeonThemeData theme, DungeonVisualStyleData style)
        {
            if (style == null)
            {
                EditorGUILayout.HelpBox("Create or select a style in the Theme tab.", MessageType.Info);
                return;
            }

            style.EnsureValid();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"{theme.Name} / Style", EditorStyles.boldLabel);
            style.Id = EditorGUILayout.IntField("Local Style Id", style.Id);
            style.Name = EditorGUILayout.TextField("Name", style.Name ?? string.Empty);
            style.StyleKey = EditorGUILayout.TextField("Style Key", style.StyleKey ?? string.Empty);

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Corridor Variants", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each style provides the three supported widths: 1, 3, and 5.", MessageType.None);
            DrawRequiredCorridor(style, 1);
            DrawRequiredCorridor(style, 3);
            DrawRequiredCorridor(style, 5);

            GUILayout.Space(8f);
            DrawAreaVisualList(style.RoomVisuals, "Room Variants");
            DrawAnteRoomVisuals(style);
            DrawTransitions(theme, style);

            if (EditorGUI.EndChangeCheck())
            {
                style.EnsureValid();
                _isDirty = true;
            }
        }

        private void DrawRequiredCorridor(DungeonVisualStyleData style, int width)
        {
            DungeonCorridorVisualData corridor = style.Corridors.FirstOrDefault(entry => entry != null && entry.Width == width);
            if (corridor == null)
            {
                corridor = new DungeonCorridorVisualData { Width = width };
                style.Corridors.Add(corridor);
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Width {width}", EditorStyles.miniBoldLabel);
            corridor.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", corridor.Weight));
            if (GUILayout.Button($"Open {width} x {width} Tile Grid"))
            {
                TileGridPreviewWindow.Open(
                    $"Corridor {width} x {width}",
                    corridor.TileGrid,
                    true,
                    () =>
                    {
                        corridor.EnsureValid();
                        _isDirty = true;
                        Repaint();
                    });
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAreaVisualList(List<DungeonAreaVisualData> visuals, string title)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add", GUILayout.Width(52f)))
                visuals.Add(new DungeonAreaVisualData());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Variants are selected by the generated region width. Multiple entries can share a range and use weights.", MessageType.None);

            for (int i = 0; i < visuals.Count; i++)
            {
                DungeonAreaVisualData visual = visuals[i] ?? new DungeonAreaVisualData();
                visuals[i] = visual;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Variant {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    visuals.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                visual.MinWidth = Mathf.Max(1, EditorGUILayout.IntField("Min Width", visual.MinWidth));
                visual.MaxWidth = Mathf.Max(visual.MinWidth, EditorGUILayout.IntField("Max Width", visual.MaxWidth));
                visual.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", visual.Weight));
                if (GUILayout.Button("Open Tile Grid"))
                {
                    TileGridPreviewWindow.Open(
                        $"{title} Variant {i + 1}",
                        visual.TileGrid,
                        false,
                        () =>
                        {
                            visual.EnsureValid();
                            _isDirty = true;
                            Repaint();
                        });
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAnteRoomVisuals(DungeonVisualStyleData style)
        {
            MigrateAnteRoomVisuals(style);
            EditorGUILayout.LabelField("Ante Room Layouts", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ante rooms only generate at 5x5 or 7x7. Add multiple layouts under either size to vary that size's result.", MessageType.None);
            DrawAnteRoomSizeLayouts(style, 5);
            DrawAnteRoomSizeLayouts(style, 7);
        }

        private void DrawAnteRoomSizeLayouts(DungeonVisualStyleData style, int sideLength)
        {
            List<DungeonAreaVisualData> layouts = style.AnteRoomVisuals
                .Where(entry => entry != null && entry.MinWidth == sideLength && entry.MaxWidth == sideLength)
                .ToList();
            if (layouts.Count == 0)
            {
                DungeonAreaVisualData layout = CreateAnteRoomLayout(sideLength);
                style.AnteRoomVisuals.Add(layout);
                layouts.Add(layout);
                _isDirty = true;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{sideLength} x {sideLength}", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add Layout", GUILayout.Width(82f)))
            {
                DungeonAreaVisualData layout = CreateAnteRoomLayout(sideLength);
                style.AnteRoomVisuals.Add(layout);
                layouts.Add(layout);
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < layouts.Count; i++)
            {
                DungeonAreaVisualData layout = layouts[i];
                layout.TileGrid.EnsureSize(sideLength, sideLength);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Layout {i + 1}", GUILayout.Width(68f));
                layout.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", layout.Weight));
                if (GUILayout.Button("Open Tile Grid", GUILayout.Width(104f)))
                {
                    TileGridPreviewWindow.Open(
                        $"Ante Room {sideLength} x {sideLength} Layout {i + 1}",
                        layout.TileGrid,
                        true,
                        () =>
                        {
                            layout.TileGrid.EnsureSize(sideLength, sideLength);
                            _isDirty = true;
                            Repaint();
                        });
                }

                GUI.enabled = layouts.Count > 1;
                if (GUILayout.Button("Delete", GUILayout.Width(54f)))
                {
                    style.AnteRoomVisuals.Remove(layout);
                    _isDirty = true;
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void MigrateAnteRoomVisuals(DungeonVisualStyleData style)
        {
            List<DungeonAreaVisualData> legacyLayouts = style.AnteRoomVisuals
                .Where(entry => entry != null && (entry.MinWidth != entry.MaxWidth || (entry.MinWidth != 5 && entry.MinWidth != 7)))
                .ToList();
            for (int i = 0; i < legacyLayouts.Count; i++)
            {
                DungeonAreaVisualData source = legacyLayouts[i];
                style.AnteRoomVisuals.Remove(source);
                style.AnteRoomVisuals.Add(CreateAnteRoomLayout(5, source));
                style.AnteRoomVisuals.Add(CreateAnteRoomLayout(7, source));
                _isDirty = true;
            }
        }

        private static DungeonAreaVisualData CreateAnteRoomLayout(int sideLength, DungeonAreaVisualData source = null)
        {
            DungeonAreaVisualData layout = source == null ? new DungeonAreaVisualData() : DeepCopy(source);
            layout.MinWidth = sideLength;
            layout.MaxWidth = sideLength;
            layout.TileGrid ??= new DungeonTileGridData();
            layout.TileGrid.EnsureSize(sideLength, sideLength);
            layout.EnsureValid();
            return layout;
        }

        private void DrawTransitions(DungeonThemeData theme, DungeonVisualStyleData style)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Child Style Weights", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add", GUILayout.Width(52f)))
                style.ChildStyleTransitions.Add(new DungeonVisualStyleTransitionData { StyleId = style.Id });
            EditorGUILayout.EndHorizontal();

            List<IntOption> options = BuildStyleOptions(theme);
            for (int i = 0; i < style.ChildStyleTransitions.Count; i++)
            {
                DungeonVisualStyleTransitionData transition = style.ChildStyleTransitions[i] ?? new DungeonVisualStyleTransitionData();
                style.ChildStyleTransitions[i] = transition;
                EditorGUILayout.BeginHorizontal();
                transition.StyleId = DrawIntPopup("Style", transition.StyleId, options);
                transition.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", transition.Weight));
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    style.ChildStyleTransitions.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawWallTileSettings(DungeonVisualStyleData style)
        {
            if (style == null)
            {
                EditorGUILayout.HelpBox("Create or select a style in the Visual Styles subpage.", MessageType.Info);
                return;
            }

            DungeonWallTileSetData wallTileSet = style.WallTileSet ??= new DungeonWallTileSetData();
            wallTileSet.EnsureValid();
            EditorGUILayout.LabelField($"{style.Name} / Wall Tiles", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The 3x3 grid represents top, bottom, left, right, and four corner wall tiles. The center is intentionally disabled because it is walkable floor space.", MessageType.None);
            if (GUILayout.Button("Open 3 x 3 Wall Tile Grid", GUILayout.Width(188f)))
            {
                TileGridPreviewWindow.Open(
                    $"{style.Name} Wall Tiles",
                    wallTileSet.TileGrid,
                    true,
                    () =>
                    {
                        wallTileSet.EnsureValid();
                        _isDirty = true;
                        Repaint();
                    },
                    isCenterCellDisabled: true);
            }
        }

        private void DrawDoorTileSettings(DungeonVisualStyleData style)
        {
            if (style == null)
            {
                EditorGUILayout.HelpBox("Create or select a style in the Visual Styles subpage.", MessageType.Info);
                return;
            }

            DungeonDoorTileSetData doorTileSet = style.DoorTileSet ??= new DungeonDoorTileSetData();
            doorTileSet.EnsureValid();
            EditorGUILayout.LabelField($"{style.Name} / Door Tiles", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Horizontal and vertical doors are selected from the dungeon's existing H_DOOR and V_DOOR map cells. Collision is configured inside each tile grid.", MessageType.None);
            DrawDoorTileButton("Horizontal Door", doorTileSet.Horizontal, style.Name);
            DrawDoorTileButton("Vertical Door", doorTileSet.Vertical, style.Name);
        }

        private void DrawDoorTileButton(string label, DungeonTileGridData tileGrid, string styleName)
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Open Tile", GUILayout.Width(82f)))
            {
                TileGridPreviewWindow.Open(
                    $"{styleName} {label}",
                    tileGrid,
                    true,
                    () =>
                    {
                        tileGrid.EnsureSize(1, 1);
                        _isDirty = true;
                        Repaint();
                    });
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTileSetSettings(DungeonVisualStyleData style)
        {
            if (style == null)
            {
                EditorGUILayout.HelpBox("Create or select a style in the Theme tab.", MessageType.Info);
                return;
            }

            DungeonTileSetData tileSet = style.TileSet ??= new DungeonTileSetData();
            tileSet.EnsureValid();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"{style.Name} / Tile Set", EditorStyles.boldLabel);
            tileSet.Name = EditorGUILayout.TextField("Name", tileSet.Name ?? string.Empty);
            tileSet.RoomMaterialPath = DrawAssetPath<Material>("Room Material", tileSet.RoomMaterialPath);
            tileSet.AnteRoomMaterialPath = DrawAssetPath<Material>("Ante Room Material", tileSet.AnteRoomMaterialPath);
            tileSet.WallMaterialPath = DrawAssetPath<Material>("Wall Material", tileSet.WallMaterialPath);
            tileSet.DoorPrefabName = DrawEnvironmentPrefabPopup("Door Prefab", tileSet.DoorPrefabName, BuildEnvironmentPrefabOptions());
            tileSet.PreviewSpritePath = DrawAssetPath<Sprite>("Preview Sprite", tileSet.PreviewSpritePath);

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Autotile Mapping", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Mapping Slots", GUILayout.Width(142f)))
            {
                EnsureTileMappingSlots(tileSet);
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Floor uses 9 mapping slots. Walls use 16 four-neighbor masks. These are style-local and ready for the future tile renderer.", MessageType.None);
            DrawFloorTileMapping(tileSet);
            DrawWallTileMapping(tileSet);

            if (EditorGUI.EndChangeCheck())
            {
                tileSet.EnsureValid();
                _isDirty = true;
            }
        }

        private void DrawFloorTileMapping(DungeonTileSetData tileSet)
        {
            EditorGUILayout.LabelField("Floor 3x3", EditorStyles.miniBoldLabel);
            Array roles = Enum.GetValues(typeof(DungeonFloorTileRole));
            for (int i = 0; i < roles.Length; i++)
            {
                DungeonFloorTileRole role = (DungeonFloorTileRole)roles.GetValue(i);
                DungeonFloorTileSpriteData mapping = tileSet.FloorTiles.FirstOrDefault(entry => entry != null && entry.Role == role);
                if (mapping == null)
                {
                    EditorGUILayout.LabelField(role.ToString(), "Not configured");
                    continue;
                }
                mapping.SpritePath = DrawAssetPath<Sprite>(role.ToString(), mapping.SpritePath);
            }
        }

        private void DrawWallTileMapping(DungeonTileSetData tileSet)
        {
            EditorGUILayout.LabelField("Wall Neighbor Masks", EditorStyles.miniBoldLabel);
            for (int mask = 0; mask < 16; mask++)
            {
                DungeonWallTileSpriteData mapping = tileSet.WallTiles.FirstOrDefault(entry => entry != null && entry.NeighborMask == mask);
                if (mapping == null)
                {
                    EditorGUILayout.LabelField($"Mask {mask:D2}", "Not configured");
                    continue;
                }
                mapping.SpritePath = DrawAssetPath<Sprite>($"Mask {mask:D2}", mapping.SpritePath);
            }
        }

        private void DrawProfileSettings(DungeonVisualStyleData style, bool isAnteRoom)
        {
            if (style == null)
            {
                EditorGUILayout.HelpBox("Create or select a style in the Theme tab.", MessageType.Info);
                return;
            }

            List<DungeonInteriorProfileData> profiles = isAnteRoom ? style.AnteRoomProfiles : style.RoomProfiles;
            int selectedIndex = isAnteRoom ? _selectedAnteProfileIndex : _selectedRoomProfileIndex;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{style.Name} / {(isAnteRoom ? "Ante Room" : "Room")} Profiles", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Profile", GUILayout.Width(92f)))
            {
                profiles.Add(new DungeonInteriorProfileData { Name = $"{(isAnteRoom ? "Ante" : "Room")} Profile {profiles.Count + 1}" });
                selectedIndex = profiles.Count - 1;
                _isDirty = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(220f));
            for (int i = 0; i < profiles.Count; i++)
            {
                DungeonInteriorProfileData profile = profiles[i] ?? new DungeonInteriorProfileData();
                profiles[i] = profile;
                if (GUILayout.Toggle(i == selectedIndex, profile.Name ?? $"Profile {i + 1}", "Button"))
                    selectedIndex = i;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DungeonInteriorProfileData selectedProfile = selectedIndex >= 0 && selectedIndex < profiles.Count ? profiles[selectedIndex] : null;
            DrawProfileDetail(profiles, ref selectedIndex, selectedProfile);
            if (isAnteRoom)
                _selectedAnteProfileIndex = selectedIndex;
            else
                _selectedRoomProfileIndex = selectedIndex;
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProfileDetail(List<DungeonInteriorProfileData> profiles, ref int selectedIndex, DungeonInteriorProfileData profile)
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Select or add a profile.", MessageType.Info);
                return;
            }

            profile.EnsureValid();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Profile", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Delete Profile", GUILayout.Width(102f)))
            {
                profiles.RemoveAt(selectedIndex);
                selectedIndex = Mathf.Clamp(selectedIndex, -1, profiles.Count - 1);
                _isDirty = true;
                EditorGUI.EndChangeCheck();
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();

            profile.Name = EditorGUILayout.TextField("Name", profile.Name ?? string.Empty);
            profile.MinWidth = Mathf.Max(1, EditorGUILayout.IntField("Min Width", profile.MinWidth));
            profile.MinHeight = Mathf.Max(1, EditorGUILayout.IntField("Min Height", profile.MinHeight));
            profile.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", profile.Weight));
            DrawMainDecorations(profile);
            DrawSecondaryDecorations(profile);
            DrawEdgeDecorations(profile);
            DrawInteriorPreview(profile);

            if (EditorGUI.EndChangeCheck())
            {
                profile.EnsureValid();
                _isDirty = true;
            }
        }

        private void DrawMainDecorations(DungeonInteriorProfileData profile)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Main Decoration", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add", GUILayout.Width(52f)))
                profile.MainDecorations.Add(new DungeonMainDecorationData());
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < profile.MainDecorations.Count; i++)
            {
                DungeonMainDecorationData entry = profile.MainDecorations[i] ?? new DungeonMainDecorationData();
                profile.MainDecorations[i] = entry;
                EditorGUILayout.BeginHorizontal();
                entry.PrefabName = DrawEnvironmentPrefabPopup("Prefab", entry.PrefabName, BuildEnvironmentPrefabOptions());
                entry.Width = Mathf.Max(1, EditorGUILayout.IntField("W", entry.Width));
                entry.Height = Mathf.Max(1, EditorGUILayout.IntField("H", entry.Height));
                entry.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", entry.Weight));
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    profile.MainDecorations.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSecondaryDecorations(DungeonInteriorProfileData profile)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Secondary Decoration", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add", GUILayout.Width(52f)))
                profile.SecondaryDecorations.Add(new DungeonSecondaryDecorationData());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("These are only placed as complete left-right pairs.", MessageType.None);
            for (int i = 0; i < profile.SecondaryDecorations.Count; i++)
            {
                DungeonSecondaryDecorationData entry = profile.SecondaryDecorations[i] ?? new DungeonSecondaryDecorationData();
                profile.SecondaryDecorations[i] = entry;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                entry.PrefabName = DrawEnvironmentPrefabPopup("Prefab", entry.PrefabName, BuildEnvironmentPrefabOptions());
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    profile.SecondaryDecorations.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                entry.Width = Mathf.Max(1, EditorGUILayout.IntField("Width", entry.Width));
                entry.Height = Mathf.Max(1, EditorGUILayout.IntField("Height", entry.Height));
                entry.MinPairs = Mathf.Max(0, EditorGUILayout.IntField("Min Pairs", entry.MinPairs));
                entry.MaxPairs = Mathf.Max(entry.MinPairs, EditorGUILayout.IntField("Max Pairs", entry.MaxPairs));
                entry.MinDistance = Mathf.Max(1, EditorGUILayout.IntField("Min Distance", entry.MinDistance));
                entry.MaxDistance = Mathf.Max(entry.MinDistance, EditorGUILayout.IntField("Max Distance", entry.MaxDistance));
                entry.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", entry.Weight));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawEdgeDecorations(DungeonInteriorProfileData profile)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Edge Decoration", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add", GUILayout.Width(52f)))
                profile.EdgeDecorations.Add(new DungeonEdgeDecorationData());
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < profile.EdgeDecorations.Count; i++)
            {
                DungeonEdgeDecorationData entry = profile.EdgeDecorations[i] ?? new DungeonEdgeDecorationData();
                profile.EdgeDecorations[i] = entry;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                entry.PrefabName = DrawEnvironmentPrefabPopup("Prefab", entry.PrefabName, BuildEnvironmentPrefabOptions());
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    profile.EdgeDecorations.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                entry.Width = Mathf.Max(1, EditorGUILayout.IntField("Footprint Width", entry.Width));
                entry.Height = Mathf.Max(1, EditorGUILayout.IntField("Footprint Height", entry.Height));
                entry.AnchorX = Mathf.Max(0, EditorGUILayout.IntField("Anchor X", entry.AnchorX));
                entry.AnchorY = Mathf.Max(0, EditorGUILayout.IntField("Anchor Y", entry.AnchorY));
                entry.AnchorWidth = Mathf.Max(1, EditorGUILayout.IntField("Anchor Width", entry.AnchorWidth));
                entry.AnchorHeight = Mathf.Max(1, EditorGUILayout.IntField("Anchor Height", entry.AnchorHeight));
                entry.AllowedAnchors = (DungeonEdgeAnchor)EditorGUILayout.EnumFlagsField("Allowed Anchors", entry.AllowedAnchors);
                entry.MaxInstances = Mathf.Max(1, EditorGUILayout.IntField("Max Instances", entry.MaxInstances));
                entry.Weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", entry.Weight));
                entry.EnsureValid();
                DrawAnchorRectPreview(entry);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawInteriorPreview(DungeonInteriorProfileData profile)
        {
            EditorGUILayout.LabelField("Layout Preview", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(360f, 220f, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(previewRect, new Color(0.1f, 0.12f, 0.12f, 1f));
            const int gridWidth = 12;
            const int gridHeight = 8;
            float cellSize = Mathf.Min(previewRect.width / gridWidth, previewRect.height / gridHeight);
            Vector2 origin = new(previewRect.x + 6f, previewRect.y + 6f);
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                    EditorGUI.DrawRect(new Rect(origin.x + x * cellSize, origin.y + y * cellSize, cellSize - 1f, cellSize - 1f), new Color(0.26f, 0.3f, 0.3f, 1f));
            }

            Vector2Int center = new(gridWidth / 2, gridHeight / 2);
            DungeonMainDecorationData main = profile.MainDecorations.FirstOrDefault(entry => entry != null);
            if (main != null)
                DrawPreviewFootprint(origin, cellSize, center.x - main.Width / 2, center.y - main.Height / 2, main.Width, main.Height, new Color(0.85f, 0.55f, 0.18f, 0.9f));
            DungeonSecondaryDecorationData secondary = profile.SecondaryDecorations.FirstOrDefault(entry => entry != null);
            if (secondary != null)
            {
                DrawPreviewFootprint(origin, cellSize, center.x - secondary.MinDistance - secondary.Width / 2, center.y - secondary.Height / 2, secondary.Width, secondary.Height, new Color(0.25f, 0.65f, 0.95f, 0.9f));
                DrawPreviewFootprint(origin, cellSize, center.x + secondary.MinDistance - secondary.Width / 2, center.y - secondary.Height / 2, secondary.Width, secondary.Height, new Color(0.25f, 0.65f, 0.95f, 0.9f));
            }
        }

        private static void DrawPreviewFootprint(Vector2 origin, float cellSize, int x, int y, int width, int height, Color color)
        {
            EditorGUI.DrawRect(new Rect(origin.x + x * cellSize, origin.y + y * cellSize, width * cellSize - 1f, height * cellSize - 1f), color);
        }

        private static void DrawAnchorRectPreview(DungeonEdgeDecorationData entry)
        {
            Rect rect = GUILayoutUtility.GetRect(160f, 80f, GUILayout.Width(160f));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));
            float cellSize = Mathf.Min(rect.width / entry.Width, rect.height / entry.Height);
            for (int y = 0; y < entry.Height; y++)
            {
                for (int x = 0; x < entry.Width; x++)
                {
                    bool isAnchor = x >= entry.AnchorX && x < entry.AnchorX + entry.AnchorWidth && y >= entry.AnchorY && y < entry.AnchorY + entry.AnchorHeight;
                    EditorGUI.DrawRect(new Rect(rect.x + x * cellSize, rect.y + (entry.Height - y - 1) * cellSize, cellSize - 1f, cellSize - 1f), isAnchor ? new Color(0.92f, 0.7f, 0.2f, 1f) : new Color(0.32f, 0.36f, 0.36f, 1f));
                }
            }
        }

        private void ValidateThemeStyles()
        {
            _themeStyleValidationMessages.Clear();
            for (int themeIndex = 0; themeIndex < _themes.Count; themeIndex++)
            {
                DungeonThemeData theme = _themes[themeIndex];
                if (theme == null)
                    continue;
                theme.EnsureValid();
                HashSet<int> styleIds = new();
                for (int styleIndex = 0; styleIndex < theme.VisualStyles.Count; styleIndex++)
                {
                    DungeonVisualStyleData style = theme.VisualStyles[styleIndex];
                    if (style == null)
                        continue;
                    style.EnsureValid();
                    if (!styleIds.Add(style.Id))
                        _themeStyleValidationMessages.Add($"Theme '{theme.Name}' has duplicate style id #{style.Id}.");
                    ValidateStyle(theme, style);
                }
                if (theme.GetVisualStyle(theme.RootVisualStyleId) == null)
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' has no valid root style.");
            }
            _statusText = _themeStyleValidationMessages.Count == 0
                ? "Style validation passed."
                : $"Style validation found {_themeStyleValidationMessages.Count} issue(s).";
        }

        private void ValidateStyle(DungeonThemeData theme, DungeonVisualStyleData style)
        {
            int[] widths = { 1, 3, 5 };
            for (int i = 0; i < widths.Length; i++)
            {
                DungeonCorridorVisualData corridor = style.Corridors.FirstOrDefault(entry => entry != null && entry.Width == widths[i]);
                if (corridor == null)
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' is missing corridor width {widths[i]}.");
                else if (!corridor.TileGrid.HasAssignedSprite())
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' corridor width {widths[i]} has no tile sprites.");
            }

            if (style.RoomVisuals.Count == 0)
                _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' has no room visual variant.");
            ValidateTileGrids(theme, style.Name, "room", style.RoomVisuals);
            ValidateAnteRoomTileGrids(theme, style);
            ValidateWallTiles(theme, style);
            ValidateDoorTiles(theme, style);
            ValidateProfiles(theme, style.Name, style.RoomProfiles);
            ValidateProfiles(theme, style.Name, style.AnteRoomProfiles);
            for (int i = 0; i < style.ChildStyleTransitions.Count; i++)
            {
                DungeonVisualStyleTransitionData transition = style.ChildStyleTransitions[i];
                if (transition != null && theme.GetVisualStyle(transition.StyleId) == null)
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' references missing child style #{transition.StyleId}.");
            }
        }

        private void ValidateTileGrids(
            DungeonThemeData theme,
            string styleName,
            string visualType,
            List<DungeonAreaVisualData> visuals)
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                DungeonAreaVisualData visual = visuals[i];
                if (visual == null || !visual.TileGrid.HasAssignedSprite())
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{styleName}' {visualType} variant {i + 1} has no tile sprites.");
            }
        }

        private void ValidateAnteRoomTileGrids(DungeonThemeData theme, DungeonVisualStyleData style)
        {
            int[] sideLengths = { 5, 7 };
            for (int sideIndex = 0; sideIndex < sideLengths.Length; sideIndex++)
            {
                int sideLength = sideLengths[sideIndex];
                List<DungeonAreaVisualData> layouts = style.AnteRoomVisuals
                    .Where(entry => entry != null && entry.MinWidth == sideLength && entry.MaxWidth == sideLength)
                    .ToList();
                if (layouts.Count == 0)
                {
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' is missing a {sideLength}x{sideLength} ante room layout.");
                    continue;
                }

                for (int i = 0; i < layouts.Count; i++)
                {
                    if (!layouts[i].TileGrid.HasAssignedSprite())
                        _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' {sideLength}x{sideLength} ante room layout {i + 1} has no tile sprites.");
                }
            }
        }

        private void ValidateWallTiles(DungeonThemeData theme, DungeonVisualStyleData style)
        {
            DungeonTileGridData tileGrid = style.WallTileSet?.TileGrid;
            if (tileGrid == null)
            {
                _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' has no wall tile grid.");
                return;
            }

            tileGrid.EnsureSize(3, 3);
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    if (column == 1 && row == 1)
                        continue;

                    if (!HasAssignedSprite(tileGrid.GetCell(column, row)))
                        _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' wall tile ({column + 1}, {row + 1}) has no sprite.");
                }
            }
        }

        private void ValidateDoorTiles(DungeonThemeData theme, DungeonVisualStyleData style)
        {
            DungeonDoorTileSetData doorTileSet = style.DoorTileSet;
            if (!HasAssignedSprite(doorTileSet?.Horizontal?.GetCell(0, 0)))
                _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' has no horizontal door tile.");
            if (!HasAssignedSprite(doorTileSet?.Vertical?.GetCell(0, 0)))
                _themeStyleValidationMessages.Add($"Theme '{theme.Name}' style '{style.Name}' has no vertical door tile.");
        }

        private static bool HasAssignedSprite(DungeonTileGridCellData cell)
        {
            return cell != null
                && !string.IsNullOrWhiteSpace(cell.SpritePath)
                && !string.IsNullOrWhiteSpace(cell.SpriteName);
        }

        private void ValidateProfiles(DungeonThemeData theme, string styleName, IEnumerable<DungeonInteriorProfileData> profiles)
        {
            foreach (DungeonInteriorProfileData profile in profiles)
            {
                if (profile == null)
                    continue;
                ValidatePrefabs(theme, $"{styleName}/{profile.Name}", profile.MainDecorations.Select(static entry => entry?.PrefabName));
                ValidatePrefabs(theme, $"{styleName}/{profile.Name}", profile.SecondaryDecorations.Select(static entry => entry?.PrefabName));
                ValidatePrefabs(theme, $"{styleName}/{profile.Name}", profile.EdgeDecorations.Select(static entry => entry?.PrefabName));
            }
        }

        private void ValidatePrefabs(DungeonThemeData theme, string sourceName, IEnumerable<string> prefabNames)
        {
            foreach (string prefabName in prefabNames)
            {
                if (!string.IsNullOrWhiteSpace(prefabName) && !IsRegisteredEnvironmentPrefab(prefabName))
                    _themeStyleValidationMessages.Add($"Theme '{theme.Name}' {sourceName} references missing environment prefab '{prefabName}'.");
            }
        }

        private void DrawThemeStyleValidationMessages()
        {
            if (_themeStyleValidationMessages.Count == 0)
                return;
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            for (int i = 0; i < _themeStyleValidationMessages.Count; i++)
                EditorGUILayout.HelpBox(_themeStyleValidationMessages[i], MessageType.Warning);
        }

        private DungeonVisualStyleData CreateStyle(DungeonThemeData theme)
        {
            DungeonVisualStyleData style = new()
            {
                Id = GetNextStyleId(theme),
                Name = $"Style {theme.VisualStyles.Count + 1}",
                StyleKey = $"style_{theme.VisualStyles.Count + 1:D2}",
            };
            style.Corridors.Add(new DungeonCorridorVisualData { Width = 1 });
            style.Corridors.Add(new DungeonCorridorVisualData { Width = 3 });
            style.Corridors.Add(new DungeonCorridorVisualData { Width = 5 });
            style.RoomVisuals.Add(new DungeonAreaVisualData());
            style.AnteRoomVisuals.Add(CreateAnteRoomLayout(5));
            style.AnteRoomVisuals.Add(CreateAnteRoomLayout(7));
            style.ChildStyleTransitions.Add(new DungeonVisualStyleTransitionData { StyleId = style.Id, Weight = 100 });
            theme.VisualStyles.Add(style);
            if (theme.RootVisualStyleId <= 0)
                theme.RootVisualStyleId = style.Id;
            _isDirty = true;
            return style;
        }

        private static int GetNextStyleId(DungeonThemeData theme)
        {
            int nextId = 1;
            for (int i = 0; i < theme.VisualStyles.Count; i++)
            {
                DungeonVisualStyleData style = theme.VisualStyles[i];
                if (style != null)
                    nextId = Mathf.Max(nextId, style.Id + 1);
            }
            return nextId;
        }

        private DungeonVisualStyleData GetSelectedStyle(DungeonThemeData theme)
        {
            return theme != null && _selectedStyleIndex >= 0 && _selectedStyleIndex < theme.VisualStyles.Count
                ? theme.VisualStyles[_selectedStyleIndex]
                : null;
        }

        private static List<IntOption> BuildStyleOptions(DungeonThemeData theme)
        {
            return theme.VisualStyles.Where(static style => style != null)
                .Select(static style => new IntOption { Id = style.Id, Label = $"[{style.Id}] {style.Name}" })
                .ToList();
        }

        private static List<StringOption> BuildEnvironmentPrefabOptions()
        {
            List<StringOption> options = new();
            if (!AssetDatabase.IsValidFolder(EnvironmentPrefabFolder))
                return options;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnvironmentPrefabFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                options.Add(new StringOption { Value = name, Label = name });
            }
            options.Sort(static (left, right) => string.Compare(left.Label, right.Label, StringComparison.Ordinal));
            return options;
        }

        private static string DrawEnvironmentPrefabPopup(string label, string value, List<StringOption> options)
        {
            value ??= string.Empty;
            List<StringOption> allOptions = new(options);
            if (!allOptions.Any(option => option.Value == value))
                allOptions.Insert(0, new StringOption { Value = value, Label = string.IsNullOrWhiteSpace(value) ? "None" : $"Missing: {value}" });
            int index = Mathf.Max(0, allOptions.FindIndex(option => option.Value == value));
            return allOptions[EditorGUILayout.Popup(label, index, allOptions.Select(static option => option.Label).ToArray())].Value;
        }

        private static string DrawAssetPath<T>(string label, string path) where T : UnityEngine.Object
        {
            T asset = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            T selected = (T)EditorGUILayout.ObjectField(label, asset, typeof(T), false);
            return selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
        }

        private static void EnsureTileMappingSlots(DungeonTileSetData tileSet)
        {
            Array roles = Enum.GetValues(typeof(DungeonFloorTileRole));
            for (int i = 0; i < roles.Length; i++)
            {
                DungeonFloorTileRole role = (DungeonFloorTileRole)roles.GetValue(i);
                if (!tileSet.FloorTiles.Any(entry => entry != null && entry.Role == role))
                    tileSet.FloorTiles.Add(new DungeonFloorTileSpriteData { Role = role });
            }
            for (int mask = 0; mask < 16; mask++)
            {
                if (!tileSet.WallTiles.Any(entry => entry != null && entry.NeighborMask == mask))
                    tileSet.WallTiles.Add(new DungeonWallTileSpriteData { NeighborMask = mask });
            }
        }

        private static bool IsRegisteredEnvironmentPrefab(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName) || !AssetDatabase.IsValidFolder(EnvironmentPrefabFolder))
                return false;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnvironmentPrefabFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), prefabName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

    }
}
