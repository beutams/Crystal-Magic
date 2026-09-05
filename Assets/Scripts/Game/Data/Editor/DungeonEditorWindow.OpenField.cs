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
            DrawVisualSettings(data.Visual, theme);
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

        private void DrawVisualSettings(OpenFieldDungeonVisualData visual, DungeonThemeData theme)
        {
            visual.EnsureValid();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Terrain Rule Tiles", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Each field stores a RuleTile asset path. Void and obstacle variants are resolved into their own runtime Tilemaps.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.LabelField("Void", EditorStyles.miniBoldLabel);
            visual.VoidVisual.AbyssRuleTile.AssetPath = DrawRuleTilePath("Abyss", visual.VoidVisual.AbyssRuleTile.AssetPath);
            visual.VoidVisual.WallRuleTile.AssetPath = DrawRuleTilePath("Wall", visual.VoidVisual.WallRuleTile.AssetPath);
            visual.VoidVisual.TransitionRuleTile.AssetPath = DrawRuleTilePath("Transition", visual.VoidVisual.TransitionRuleTile.AssetPath);

            GUILayout.Space(3f);
            EditorGUILayout.LabelField("Obstacle", EditorStyles.miniBoldLabel);
            visual.ObstacleVisual.TopRuleTile.AssetPath = DrawRuleTilePath("Top", visual.ObstacleVisual.TopRuleTile.AssetPath);
            visual.ObstacleVisual.WallRuleTile.AssetPath = DrawRuleTilePath("Wall", visual.ObstacleVisual.WallRuleTile.AssetPath);
            visual.ObstacleVisual.TransitionRuleTile.AssetPath = DrawRuleTilePath("Transition", visual.ObstacleVisual.TransitionRuleTile.AssetPath);

            GUILayout.Space(4f);
            visual.GroundCellsPerStyleSeed = Mathf.Max(1, EditorGUILayout.IntField("Ground Cells Per Style Seed", visual.GroundCellsPerStyleSeed));
            DrawGroundStyles(visual, theme);
            EditorGUILayout.EndVertical();
        }

        private static string DrawRuleTilePath(string label, string path)
        {
            RuleTile current = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            RuleTile next = (RuleTile)EditorGUILayout.ObjectField(label, current, typeof(RuleTile), false);
            return next == null ? string.Empty : AssetDatabase.GetAssetPath(next);
        }

        private void DrawGroundStyles(OpenFieldDungeonVisualData visual, DungeonThemeData theme)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ground Styles", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add Ground Style", GUILayout.Width(118f)))
                visual.GroundStyles.Add(new OpenFieldGroundStyleData { Name = $"Ground Style {visual.GroundStyles.Count + 1}" });
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Every ground region is divided among these styles before their decorations and obstacles are placed.", EditorStyles.wordWrappedMiniLabel);

            for (int styleIndex = 0; styleIndex < visual.GroundStyles.Count; styleIndex++)
            {
                OpenFieldGroundStyleData style = visual.GroundStyles[styleIndex] ??= new OpenFieldGroundStyleData();
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.BeginHorizontal();
                string label = string.IsNullOrWhiteSpace(style.Name) ? $"Ground Style {styleIndex + 1}" : style.Name;
                bool expanded = DrawSectionFoldout(GetSectionFoldoutKey(theme, "openfield-ground-style", styleIndex), label);
                bool delete = GUILayout.Button("Delete", GUILayout.Width(56f));
                EditorGUILayout.EndHorizontal();
                if (delete)
                {
                    visual.GroundStyles.RemoveAt(styleIndex);
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (expanded)
                {
                    style.Name = EditorGUILayout.TextField("Name", style.Name ?? string.Empty);
                    style.BaseRuleTile.AssetPath = DrawRuleTilePath("Base Rule Tile", style.BaseRuleTile.AssetPath);
                    DrawDecorations(style, theme, styleIndex);
                    DrawObstacles(style, theme, styleIndex);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawDecorations(OpenFieldGroundStyleData style, DungeonThemeData theme, int styleIndex)
        {
            GUILayout.Space(3f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Decorations", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add Decoration", GUILayout.Width(110f)))
                style.Decorations.Add(new OpenFieldDecorationData { Name = $"Decoration {style.Decorations.Count + 1}" });
            EditorGUILayout.EndHorizontal();

            for (int decorationIndex = 0; decorationIndex < style.Decorations.Count; decorationIndex++)
            {
                OpenFieldDecorationData decoration = style.Decorations[decorationIndex] ??= new OpenFieldDecorationData();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                string label = string.IsNullOrWhiteSpace(decoration.Name) ? $"Decoration {decorationIndex + 1}" : decoration.Name;
                bool expanded = DrawSectionFoldout(GetSectionFoldoutKey(theme, $"openfield-decoration-{styleIndex}", decorationIndex), label);
                bool delete = GUILayout.Button("Delete", GUILayout.Width(56f));
                EditorGUILayout.EndHorizontal();
                if (delete)
                {
                    style.Decorations.RemoveAt(decorationIndex);
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (expanded)
                {
                    decoration.Name = EditorGUILayout.TextField("Name", decoration.Name ?? string.Empty);
                    decoration.RuleTile.AssetPath = DrawRuleTilePath("Rule Tile", decoration.RuleTile.AssetPath);
                    decoration.Radius = Mathf.Max(0.01f, EditorGUILayout.FloatField("Radius", decoration.Radius));
                    decoration.MaximumSpread = Mathf.Max(0, EditorGUILayout.IntField("Maximum Spread", decoration.MaximumSpread));
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawObstacles(OpenFieldGroundStyleData style, DungeonThemeData theme, int styleIndex)
        {
            GUILayout.Space(3f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Obstacles", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add Obstacle", GUILayout.Width(100f)))
                style.Obstacles.Add(new OpenFieldObstacleData { Name = $"Obstacle {style.Obstacles.Count + 1}" });
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Only checked collision-mask cells block movement. Each checked cell requires one clear Ground cell around it.", EditorStyles.wordWrappedMiniLabel);

            for (int obstacleIndex = 0; obstacleIndex < style.Obstacles.Count; obstacleIndex++)
            {
                OpenFieldObstacleData obstacle = style.Obstacles[obstacleIndex] ??= new OpenFieldObstacleData();
                obstacle.EnsureValid();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                string label = string.IsNullOrWhiteSpace(obstacle.Name) ? $"Obstacle {obstacleIndex + 1}" : obstacle.Name;
                bool expanded = DrawSectionFoldout(GetSectionFoldoutKey(theme, $"openfield-obstacle-{styleIndex}", obstacleIndex), label);
                bool delete = GUILayout.Button("Delete", GUILayout.Width(56f));
                EditorGUILayout.EndHorizontal();
                if (delete)
                {
                    style.Obstacles.RemoveAt(obstacleIndex);
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (expanded)
                {
                    obstacle.Name = EditorGUILayout.TextField("Name", obstacle.Name ?? string.Empty);
                    DrawSpriteReference("Sprite", obstacle.Sprite);
                    int width = Mathf.Max(1, EditorGUILayout.IntField("Footprint Width", obstacle.FootprintWidth));
                    int height = Mathf.Max(1, EditorGUILayout.IntField("Footprint Height", obstacle.FootprintHeight));
                    if (width != obstacle.FootprintWidth || height != obstacle.FootprintHeight)
                    {
                        obstacle.FootprintWidth = width;
                        obstacle.FootprintHeight = height;
                        obstacle.EnsureValid();
                    }

                    obstacle.Weight = Mathf.Max(1, EditorGUILayout.IntField("Probability Weight", obstacle.Weight));
                    obstacle.MinimumSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Minimum Spacing", obstacle.MinimumSpacing));
                    obstacle.MaximumCount = Mathf.Max(0, EditorGUILayout.IntField("Maximum Count", obstacle.MaximumCount));
                    obstacle.AllowRotation = EditorGUILayout.Toggle("Allow Rotation", obstacle.AllowRotation);
                    obstacle.AllowFlipX = EditorGUILayout.Toggle("Allow Flip X", obstacle.AllowFlipX);
                    obstacle.VisualSortAnchor = EditorGUILayout.Vector2Field("Visual Sort Anchor", obstacle.VisualSortAnchor);
                    DrawCollisionMask(obstacle);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawSpriteReference(string label, OpenFieldSpriteReferenceData reference)
        {
            reference ??= new OpenFieldSpriteReferenceData();
            Sprite current = LoadSprite(reference);
            Sprite next = (Sprite)EditorGUILayout.ObjectField(label, current, typeof(Sprite), false);
            if (next == current)
                return;

            if (next == null)
            {
                reference.AssetPath = string.Empty;
                reference.SpriteName = string.Empty;
                reference.SpriteUv = Vector4.zero;
                reference.HasSpriteUv = false;
                return;
            }

            reference.AssetPath = AssetDatabase.GetAssetPath(next);
            reference.SpriteName = next.name;
            Texture2D texture = next.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                reference.SpriteUv = Vector4.zero;
                reference.HasSpriteUv = false;
                return;
            }

            Rect rect = next.textureRect;
            reference.SpriteUv = new Vector4(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);
            reference.HasSpriteUv = true;
        }

        private static Sprite LoadSprite(OpenFieldSpriteReferenceData reference)
        {
            if (reference == null
                || string.IsNullOrWhiteSpace(reference.AssetPath)
                || string.IsNullOrWhiteSpace(reference.SpriteName))
            {
                return null;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(reference.AssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == reference.SpriteName)
                    return sprite;
            }

            return null;
        }

        private static void DrawCollisionMask(OpenFieldObstacleData obstacle)
        {
            obstacle.EnsureValid();
            EditorGUILayout.LabelField("Collision Mask", EditorStyles.miniBoldLabel);
            for (int y = obstacle.FootprintHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < obstacle.FootprintWidth; x++)
                {
                    int index = y * obstacle.FootprintWidth + x;
                    obstacle.CollisionMask[index] = GUILayout.Toggle(obstacle.CollisionMask[index], GUIContent.none, GUILayout.Width(20f));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
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
