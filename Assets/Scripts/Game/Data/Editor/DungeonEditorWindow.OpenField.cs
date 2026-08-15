using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public partial class DungeonEditorWindow
    {
        private void DrawOpenFieldDetailPanel()
        {
            DungeonThemeData theme = GetSelectedTheme();
            if (theme == null)
            {
                EditorGUILayout.HelpBox("Select one dungeon theme.", MessageType.Info);
                return;
            }

            theme.EnsureValid();
            OpenFieldDungeonThemeData data = theme.OpenField;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"{theme.Name} Open Field", EditorStyles.boldLabel);

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Terrain Tiles", EditorStyles.boldLabel);
            DrawTerrainGridButton("Void", data.Visual.VoidTileGrid, theme);
            DrawTerrainGridButton("Ground", data.Visual.GroundTileGrid, theme);
            DrawTerrainGridButton("Obstacle", data.Visual.ObstacleTileGrid, theme);
            DrawGenerationSettings(data);

            DrawLandmarks(data, theme);
            DrawEncounterPools(data, theme);
            DrawTreasureItems(data);
            if (EditorGUI.EndChangeCheck())
            {
                data.EnsureValid();
                _isDirty = true;
            }
        }

        private void DrawTerrainGridButton(string label, DungeonTileGridData grid, DungeonThemeData theme)
        {
            grid.EnsureSize(3, 3);
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField($"{label} 3 x 3 Tile Grid", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Open Tile Grid", GUILayout.Width(108f)))
            {
                TileGridPreviewWindow.Open(
                    $"{theme.Name} Open Field {label}",
                    grid,
                    false,
                    () =>
                    {
                        grid.EnsureSize(3, 3);
                        _isDirty = true;
                        Repaint();
                    });
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawGenerationSettings(OpenFieldDungeonThemeData data)
        {
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The map dimensions come from Dungeon Generation Config. These values are per-theme.", EditorStyles.miniLabel);
            data.Terrain.LowToGroundThreshold = EditorGUILayout.Slider("Void To Ground", data.Terrain.LowToGroundThreshold, 0.01f, 0.98f);
            data.Terrain.GroundToObstacleThreshold = EditorGUILayout.Slider("Ground To Obstacle", data.Terrain.GroundToObstacleThreshold, data.Terrain.LowToGroundThreshold + 0.01f, 0.99f);
            data.Terrain.MediumFrequencyMultiplier = EditorGUILayout.FloatField("Medium Frequency", data.Terrain.MediumFrequencyMultiplier);
            data.Terrain.MediumAmplitude = EditorGUILayout.FloatField("Medium Amplitude", data.Terrain.MediumAmplitude);
            data.Terrain.DetailFrequencyMultiplier = EditorGUILayout.FloatField("Detail Frequency", data.Terrain.DetailFrequencyMultiplier);
            data.Terrain.DetailAmplitude = EditorGUILayout.FloatField("Detail Amplitude", data.Terrain.DetailAmplitude);

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Anchors", EditorStyles.miniBoldLabel);
            data.Anchors.EntranceRadius = Mathf.Max(1, EditorGUILayout.IntField("Entrance Radius", data.Anchors.EntranceRadius));
            data.Anchors.SmallRadius = Mathf.Max(1, EditorGUILayout.IntField("Small Point Radius", data.Anchors.SmallRadius));
            data.Anchors.MediumRadius = Mathf.Max(data.Anchors.SmallRadius, EditorGUILayout.IntField("Medium Point Radius", data.Anchors.MediumRadius));
            data.Anchors.LargeRadius = Mathf.Max(data.Anchors.MediumRadius, EditorGUILayout.IntField("Large Point Radius", data.Anchors.LargeRadius));
            data.Anchors.BorderPadding = Mathf.Max(1, EditorGUILayout.IntField("Border Padding", data.Anchors.BorderPadding));
            data.Anchors.PointGap = Mathf.Max(0, EditorGUILayout.IntField("Point Gap", data.Anchors.PointGap));

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Content", EditorStyles.miniBoldLabel);
            data.Content.ChestCounts = EditorGUILayout.Vector3IntField("Chest Counts S/M/L", data.Content.ChestCounts);
            data.Content.WildSquadCount = Mathf.Max(0, EditorGUILayout.IntField("Wild Squad Count", data.Content.WildSquadCount));
            EditorGUILayout.EndVertical();
        }
        private void DrawLandmarks(OpenFieldDungeonThemeData data, DungeonThemeData theme)
        {
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Interest Point Landmarks", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Landmark", GUILayout.Width(104f)))
                data.Landmarks.Add(new OpenFieldDungeonLandmarkEntryData());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("One weighted landmark set is chosen for each interest point.", EditorStyles.miniLabel);

            for (int i = 0; i < data.Landmarks.Count; i++)
            {
                OpenFieldDungeonLandmarkEntryData entry = data.Landmarks[i] ??= new OpenFieldDungeonLandmarkEntryData();
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Landmark {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    data.Landmarks.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                entry.PrefabName = EditorGUILayout.TextField("Prefab Name", entry.PrefabName ?? string.Empty);
                entry.Weight = Mathf.Max(1, EditorGUILayout.IntField("Probability Weight", entry.Weight));
                entry.FootprintWidth = Mathf.Max(1, EditorGUILayout.IntField("Footprint Width", entry.FootprintWidth));
                entry.FootprintHeight = Mathf.Max(1, EditorGUILayout.IntField("Footprint Height", entry.FootprintHeight));
                entry.MinInstances = Mathf.Max(0, EditorGUILayout.IntField("Min Instances", entry.MinInstances));
                entry.MaxInstances = Mathf.Max(entry.MinInstances, EditorGUILayout.IntField("Max Instances", entry.MaxInstances));
                entry.ApplyCollider = EditorGUILayout.Toggle("Apply Collider", entry.ApplyCollider);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawEncounterPools(OpenFieldDungeonThemeData data, DungeonThemeData theme)
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Encounter Pools", EditorStyles.boldLabel);
            foreach (OpenFieldInterestSizeData size in new[]
                     {
                         OpenFieldInterestSizeData.Small,
                         OpenFieldInterestSizeData.Medium,
                         OpenFieldInterestSizeData.Large,
                     })
            {
                string foldoutKey = GetSectionFoldoutKey(theme, "openfield-squads", (int)size);
                EditorGUILayout.BeginVertical("box");
                if (!DrawSectionFoldout(foldoutKey, $"{size} Interest Point Squads"))
                {
                    EditorGUILayout.EndVertical();
                    continue;
                }

                OpenFieldDungeonEncounterPoolData pool = GetOrCreateOpenFieldPool(data, size);
                if (GUILayout.Button("Add Squad", GUILayout.Width(86f)))
                    pool.Squads.Add(new OpenFieldDungeonSquadData { Name = $"{size} Squad" });

                for (int i = 0; i < pool.Squads.Count; i++)
                {
                    pool.Squads[i] ??= new OpenFieldDungeonSquadData();
                    DrawOpenFieldSquad(pool.Squads[i], size == OpenFieldInterestSizeData.Large, i, pool.Squads);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawOpenFieldSquad(
            OpenFieldDungeonSquadData squad,
            bool canBeBoss,
            int index,
            List<OpenFieldDungeonSquadData> owner)
        {
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            squad.Name = EditorGUILayout.TextField(squad.Name ?? string.Empty);
            if (GUILayout.Button("Delete", GUILayout.Width(56f)))
            {
                owner.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            squad.Weight = Mathf.Max(1, EditorGUILayout.IntField("Probability Weight", squad.Weight));
            squad.MonsterLevel = Mathf.Clamp(EditorGUILayout.IntField("Monster Level", squad.MonsterLevel), 1, 3);
            EditorGUILayout.BeginHorizontal();
            squad.Width = Mathf.Max(1, EditorGUILayout.IntField("Deployment Width", squad.Width));
            squad.Height = Mathf.Max(1, EditorGUILayout.IntField("Deployment Height", squad.Height));
            EditorGUILayout.EndHorizontal();
            squad.IsBossSquad = canBeBoss && EditorGUILayout.Toggle("Boss Squad", squad.IsBossSquad);

            squad.Members ??= new List<OpenFieldDungeonSquadMemberData>();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Members", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add Member", GUILayout.Width(92f)))
                squad.Members.Add(new OpenFieldDungeonSquadMemberData());
            EditorGUILayout.EndHorizontal();

            List<StringOption> options = BuildUnitOptions();
            for (int i = 0; i < squad.Members.Count; i++)
            {
                OpenFieldDungeonSquadMemberData member = squad.Members[i] ??= new OpenFieldDungeonSquadMemberData();
                EditorGUILayout.BeginHorizontal();
                member.UnitName = DrawStringPopup("", member.UnitName, options);
                member.Count = Mathf.Max(1, EditorGUILayout.IntField(member.Count, GUILayout.Width(52f)));
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    squad.Members.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTreasureItems(OpenFieldDungeonThemeData data)
        {
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Chest Candidate Items", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Item", GUILayout.Width(80f)))
                data.TreasureItemIds.Add(-1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "Global chest luck range and rarity weights are configured in Dungeon Config.",
                EditorStyles.wordWrappedMiniLabel);

            List<IntOption> options = BuildItemOptions();
            for (int i = 0; i < data.TreasureItemIds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                data.TreasureItemIds[i] = DrawIntPopup("Item", data.TreasureItemIds[i], options);
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    data.TreasureItemIds.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        private static OpenFieldDungeonEncounterPoolData GetOrCreateOpenFieldPool(
            OpenFieldDungeonThemeData data,
            OpenFieldInterestSizeData size)
        {
            foreach (OpenFieldDungeonEncounterPoolData pool in data.EncounterPools)
                if (pool != null && pool.InterestSize == size)
                    return pool;

            OpenFieldDungeonEncounterPoolData created = new()
            {
                Name = size.ToString(),
                InterestSize = size,
            };
            data.EncounterPools.Add(created);
            return created;
        }
    }
}
