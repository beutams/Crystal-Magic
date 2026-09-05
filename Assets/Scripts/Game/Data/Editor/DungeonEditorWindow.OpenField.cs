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
                    int width = Mathf.Max(1, EditorGUILayout.IntField("Footprint Width", obstacle.FootprintWidth));
                    int height = Mathf.Max(1, EditorGUILayout.IntField("Footprint Height", obstacle.FootprintHeight));
                    if (width != obstacle.FootprintWidth || height != obstacle.FootprintHeight)
                    {
                        obstacle.FootprintWidth = width;
                        obstacle.FootprintHeight = height;
                        obstacle.EnsureValid();
                    }

                    DrawObstacleSpriteLayers(obstacle);
                    obstacle.Weight = Mathf.Max(1, EditorGUILayout.IntField("Probability Weight", obstacle.Weight));
                    obstacle.MinimumSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Minimum Spacing", obstacle.MinimumSpacing));
                    obstacle.MaximumCount = Mathf.Max(0, EditorGUILayout.IntField("Maximum Count", obstacle.MaximumCount));
                    obstacle.AllowRotation = EditorGUILayout.Toggle("Allow Rotation", obstacle.AllowRotation);
                    obstacle.AllowFlipX = EditorGUILayout.Toggle("Allow Flip X", obstacle.AllowFlipX);
                    Vector2 sortAnchor = EditorGUILayout.Vector2Field("Visual Sort Anchor", obstacle.VisualSortAnchor.ToVector2());
                    obstacle.VisualSortAnchor = new OpenFieldVector2Data(sortAnchor.x, sortAnchor.y);
                    DrawCollisionMask(obstacle);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawObstacleSpriteLayers(OpenFieldObstacleData obstacle)
        {
            obstacle.EnsureValid();
            GUILayout.Space(3f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sprite Layers (Back To Front)", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Add Layer", GUILayout.Width(84f)))
            {
                obstacle.SpriteLayers.Add(new OpenFieldObstacleSpriteLayerData
                {
                    Name = $"Layer {obstacle.SpriteLayers.Count + 1}",
                });
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Drag Sprite assets into a cell. Later layers draw in front; the collision mask remains independent.", EditorStyles.wordWrappedMiniLabel);

            for (int layerIndex = 0; layerIndex < obstacle.SpriteLayers.Count; layerIndex++)
            {
                OpenFieldObstacleSpriteLayerData layer = obstacle.SpriteLayers[layerIndex] ??= new OpenFieldObstacleSpriteLayerData();
                layer.EnsureValid();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Layer {layerIndex + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(54f));
                layer.Name = EditorGUILayout.TextField(layer.Name ?? string.Empty);
                bool moveBackward = GUILayout.Button("↑", GUILayout.Width(24f));
                bool moveForward = GUILayout.Button("↓", GUILayout.Width(24f));
                bool delete = GUILayout.Button("Delete", GUILayout.Width(50f));
                EditorGUILayout.EndHorizontal();

                if (delete)
                {
                    obstacle.SpriteLayers.RemoveAt(layerIndex);
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (moveBackward && layerIndex > 0)
                {
                    (obstacle.SpriteLayers[layerIndex - 1], obstacle.SpriteLayers[layerIndex]) =
                        (obstacle.SpriteLayers[layerIndex], obstacle.SpriteLayers[layerIndex - 1]);
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (moveForward && layerIndex < obstacle.SpriteLayers.Count - 1)
                {
                    (obstacle.SpriteLayers[layerIndex + 1], obstacle.SpriteLayers[layerIndex]) =
                        (obstacle.SpriteLayers[layerIndex], obstacle.SpriteLayers[layerIndex + 1]);
                    EditorGUILayout.EndVertical();
                    break;
                }

                DrawObstacleSpriteLayerGrid(obstacle, layer);
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawObstacleSpriteLayerGrid(OpenFieldObstacleData obstacle, OpenFieldObstacleSpriteLayerData layer)
        {
            const float cellSize = 46f;
            for (int y = obstacle.FootprintHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < obstacle.FootprintWidth; x++)
                {
                    Rect rect = GUILayoutUtility.GetRect(cellSize, cellSize, GUILayout.Width(cellSize), GUILayout.Height(cellSize));
                    DrawSpriteDropCell(layer, x, y, rect);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawSpriteDropCell(OpenFieldObstacleSpriteLayerData layer, int x, int y, Rect rect)
        {
            OpenFieldObstacleSpriteCellData cell = FindSpriteCell(layer, x, y);
            Sprite sprite = LoadSprite(cell?.Sprite);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            GUI.Box(rect, GUIContent.none);
            if (sprite == null || sprite.texture == null)
            {
                GUI.Label(rect, "Drop", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                Rect previewBounds = new(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
                Rect previewCanvas = GetSpritePreviewCanvasRect(sprite, previewBounds);
                Rect sourceRect = sprite.rect;
                Rect textureRect = sprite.textureRect;
                Vector2 textureRectOffset = sprite.textureRectOffset;
                float sourceWidth = Mathf.Max(1f, sourceRect.width);
                float sourceHeight = Mathf.Max(1f, sourceRect.height);
                float scale = previewCanvas.width / sourceWidth;
                // Sprite offsets use a lower-left origin; IMGUI rectangles use a top-left origin.
                Rect previewContent = new(
                    previewCanvas.x + textureRectOffset.x * scale,
                    previewCanvas.y + (sourceHeight - textureRectOffset.y - textureRect.height) * scale,
                    textureRect.width * scale,
                    textureRect.height * scale);
                Rect uv = new(
                    textureRect.x / sprite.texture.width,
                    textureRect.y / sprite.texture.height,
                    textureRect.width / sprite.texture.width,
                    textureRect.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(previewContent, sprite.texture, uv, true);
                Rect clearRect = new(rect.xMax - 17f, rect.y + 1f, 16f, 16f);
                if (GUI.Button(clearRect, "×", EditorStyles.miniButton))
                    RemoveSpriteCell(layer, x, y);
            }

            Event currentEvent = Event.current;
            if (!rect.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.type == EventType.DragUpdated && TryGetDraggedSprite(out _))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.DragPerform && TryGetDraggedSprite(out Sprite draggedSprite))
            {
                DragAndDrop.AcceptDrag();
                SetSpriteCell(layer, x, y, draggedSprite);
                currentEvent.Use();
            }
        }

        private static Rect GetSpritePreviewCanvasRect(Sprite sprite, Rect previewBounds)
        {
            float sourceWidth = Mathf.Max(1f, sprite.rect.width);
            float sourceHeight = Mathf.Max(1f, sprite.rect.height);
            float scale = Mathf.Min(previewBounds.width / sourceWidth, previewBounds.height / sourceHeight);
            float width = sourceWidth * scale;
            float height = sourceHeight * scale;
            return new Rect(
                previewBounds.x + (previewBounds.width - width) * 0.5f,
                previewBounds.y + (previewBounds.height - height) * 0.5f,
                width,
                height);
        }

        private static bool TryGetDraggedSprite(out Sprite sprite)
        {
            sprite = null;
            foreach (UnityEngine.Object candidate in DragAndDrop.objectReferences)
            {
                if (candidate is Sprite draggedSprite)
                {
                    if (sprite != null)
                    {
                        sprite = null;
                        return false;
                    }

                    sprite = draggedSprite;
                }
            }

            return sprite != null;
        }

        private static OpenFieldObstacleSpriteCellData FindSpriteCell(OpenFieldObstacleSpriteLayerData layer, int x, int y)
        {
            for (int index = 0; index < layer.Cells.Count; index++)
            {
                OpenFieldObstacleSpriteCellData cell = layer.Cells[index];
                if (cell != null && cell.X == x && cell.Y == y)
                    return cell;
            }

            return null;
        }

        private static void SetSpriteCell(OpenFieldObstacleSpriteLayerData layer, int x, int y, Sprite sprite)
        {
            OpenFieldObstacleSpriteCellData cell = FindSpriteCell(layer, x, y);
            if (cell == null)
            {
                cell = new OpenFieldObstacleSpriteCellData { X = x, Y = y };
                layer.Cells.Add(cell);
            }

            cell.UseObstacleCenter = false;
            SetSpriteReference(cell.Sprite, sprite);
        }

        private static void RemoveSpriteCell(OpenFieldObstacleSpriteLayerData layer, int x, int y)
        {
            for (int index = layer.Cells.Count - 1; index >= 0; index--)
            {
                OpenFieldObstacleSpriteCellData cell = layer.Cells[index];
                if (cell != null && cell.X == x && cell.Y == y)
                    layer.Cells.RemoveAt(index);
            }
        }

        private static void SetSpriteReference(OpenFieldSpriteReferenceData reference, Sprite sprite)
        {
            if (reference == null)
                return;

            if (sprite == null)
            {
                reference.AssetPath = string.Empty;
                reference.SpriteName = string.Empty;
                reference.SpriteUv = default;
                reference.HasSpriteUv = false;
                return;
            }

            reference.AssetPath = AssetDatabase.GetAssetPath(sprite);
            reference.SpriteName = sprite.name;
            Texture2D texture = sprite.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                reference.SpriteUv = default;
                reference.HasSpriteUv = false;
                return;
            }

            Rect textureRect = sprite.textureRect;
            reference.SpriteUv = new OpenFieldSpriteUvData(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
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
