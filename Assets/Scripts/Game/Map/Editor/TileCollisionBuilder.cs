using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Game.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace CrystalMagic.Editor.Map
{
    public static class TileCollisionBuilder
    {
        public static string ApplyRules(TileOcclusionMap map)
        {
            TileOcclusionBuilder.ValidateTarget(map);
            if (map.Regions.Count == 0) throw new InvalidOperationException("先生成遮挡分组。");
            Undo.RecordObject(map, "Generate collision footprints");
            foreach (TileOcclusionRegion region in map.Regions)
            {
                if (region.CollisionMode == TileCollisionMode.Manual || region.CollisionMode == TileCollisionMode.None) continue;
                region.CollisionReviewNote = "";
                if (region.Category == "房屋")
                {
                    region.CollisionMode = TileCollisionMode.HouseFrame;
                    region.CollisionCells = TileHouseFootprintBuilder.Build(region.Cells
                        .Where(IsHouseFrame).Select(c => c.Position));
                    continue;
                }
                IEnumerable<TileOcclusionCell> selected;
                if (region.Category == "树木")
                {
                    region.CollisionMode = TileCollisionMode.TreeTrunk;
                    // Tall trees in this map combine supplementary canopies and
                    // a Village stump. Rows 13/14 are its central body; row 15
                    // is only a tiny root/shadow fringe and must not block grass.
                    bool hasTrunk = region.Cells.Any(c => IsVillage(c, out int col, out int row) && col <= 2 && row >= 13 && row <= 15);
                    selected = hasTrunk ? region.Cells.Where(c => IsVillage(c, out int col, out int row) && col == 1 && (row == 13 || row == 14))
                        : region.Cells.Where(IsTreeCentreBottom);
                }
                else if (region.Category == "木桩")
                {
                    region.CollisionMode = TileCollisionMode.StumpCore;
                    // The large stump is a five-cell cross, but only 449 is its
                    // solid centre. 417/448/450/481 are fringe, leaves and roots.
                    selected = region.Cells.Where(c => IsVillage(c, out int col, out int row) &&
                        (row * 32 + col == 449 || row * 32 + col == 416 || row * 32 + col == 418 ||
                         row * 32 + col == 480 || row * 32 + col == 482));
                }
                else if (region.Cells.Any(IsWell))
                {
                    region.CollisionMode = TileCollisionMode.WellBase;
                    int bottom = region.Cells.Where(IsWell).Min(c => c.Position.y);
                    selected = region.Cells.Where(c => IsWell(c) && c.Position.y == bottom);
                }
                else
                {
                    region.CollisionMode = TileCollisionMode.Full;
                    selected = region.Cells;
                }
                region.CollisionCells = Sort(selected.Select(c => c.Position));
                if ((region.CollisionMode == TileCollisionMode.TreeTrunk || region.CollisionMode == TileCollisionMode.WellBase) && region.CollisionCells.Count != 2)
                    region.CollisionReviewNote = "边缘截断或特殊拼法：未找到完整的两格占地，请查看红色范围。";
                if (region.CollisionCells.Count == 0)
                    region.CollisionReviewNote = IsEdgeFootprint(map, region)
                        ? "地图边缘只露出树冠或树桩外围，未找到图内主体格，未添加碰撞。"
                        : "未找到碰撞占地，需手工指定后才能导出。";
            }
            MarkChanged(map);
            int cells = map.Regions.SelectMany(r => r.CollisionCells).Distinct().Count();
            int review = map.Regions.Count(r => !string.IsNullOrEmpty(r.CollisionReviewNote));
            return $"碰撞占地 {cells} 格；树干中间竖向两格、树桩主体一格、井底两格、底层房框补闭合并填满内部（忽略屋顶）、其余物件全格；待复核 {review} 组。";
        }

        private static bool IsHouseFrame(TileOcclusionCell cell)
        {
            // Roof/facade layers can contain visually similar wooden tiles but
            // must never move the footprint. Only the ground-plan frame counts.
            return cell.Source != null && !cell.Source.name.Contains("屋顶") &&
                IsVillage(cell, out int col, out int row) && col <= 7 && row >= 6 && row <= 9;
        }

        private static bool IsTreeCentreBottom(TileOcclusionCell cell)
        {
            Sprite sprite = cell.Source.GetSprite(cell.Position);
            if (sprite == null) return false;
            GetAtlas(sprite, out string atlas, out int col, out int row);
            var rule = TileOcclusionVillageRules.Classify(atlas, col, row, cell.Source.name);
            return rule.Category == TileOcclusionVillageRules.Kind.Tree && rule.Template.width > 0 &&
                col == rule.Template.x + rule.Template.width / 2 && row >= rule.Template.yMax - 2;
        }

        private static bool IsVillage(TileOcclusionCell cell, out int col, out int row)
        {
            col = row = -1;
            Sprite sprite = cell.Source != null ? cell.Source.GetSprite(cell.Position) : null;
            if (sprite == null) return false;
            GetAtlas(sprite, out string atlas, out col, out row);
            return atlas == "Village_Tileset";
        }

        private static bool IsWell(TileOcclusionCell c) => IsVillage(c, out int col, out int row) &&
            col >= 3 && col <= 4 && row >= 14 && row <= 16;

        private static void GetAtlas(Sprite sprite, out string atlas, out int col, out int row)
        {
            atlas = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sprite.texture));
            col = Mathf.RoundToInt(sprite.rect.x / sprite.rect.width);
            row = Mathf.RoundToInt((sprite.texture.height - sprite.rect.yMax) / sprite.rect.height);
        }

        private static List<Vector3Int> Sort(IEnumerable<Vector3Int> cells) => cells.Distinct().OrderBy(c => c.y).ThenBy(c => c.x).ToList();

        private static bool IsEdgeFootprint(TileOcclusionMap map, TileOcclusionRegion region)
        {
            if (region.Category != "树木" && region.Category != "木桩") return false;
            var sources = TileOcclusionBuilder.FindSources(map);
            int minX = sources.Min(t => t.cellBounds.xMin), maxX = sources.Max(t => t.cellBounds.xMax) - 1;
            int minY = sources.Min(t => t.cellBounds.yMin), maxY = sources.Max(t => t.cellBounds.yMax) - 1;
            return region.Cells.Any(c => c.Position.x == minX || c.Position.x == maxX || c.Position.y == minY || c.Position.y == maxY);
        }

        public static Tilemap ReferenceGrid(TileOcclusionMap map)
        {
            var sources = TileOcclusionBuilder.FindSources(map);
            if (sources.Length == 0) throw new InvalidOperationException("未找到源 Tilemap。");
            return sources.FirstOrDefault(t => t.name == "路面房框") ?? sources[0];
        }

        public static void Paint(TileOcclusionMap map, TileOcclusionRegion region, Vector3 start, Vector3 end, bool add)
        {
            Tilemap grid = ReferenceGrid(map);
            Vector3Int a = grid.WorldToCell(start), b = grid.WorldToCell(end);
            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 250000) throw new InvalidOperationException("碰撞框选范围过大。");
            Undo.RecordObject(map, "Paint collision footprint");
            var cells = new HashSet<Vector3Int>(region.CollisionCells);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (add) cells.Add(new Vector3Int(x, y, 0)); else cells.Remove(new Vector3Int(x, y, 0));
            region.CollisionCells = Sort(cells);
            region.CollisionMode = TileCollisionMode.Manual;
            region.AutoGenerated = false; // Preserve this group across auto regrouping.
            region.CollisionReviewNote = "";
            MarkChanged(map);
        }

        public static void MarkChanged(TileOcclusionMap map)
        {
            map.CollisionNeedsRebuild = true;
            EditorUtility.SetDirty(map);
            PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        }

        public static TileCollisionData CreateData(TileOcclusionMap map)
        {
            TileOcclusionBuilder.ValidateTarget(map);
            if (map.Regions.Count == 0 || map.Regions.Any(r => r.CollisionMode == TileCollisionMode.Unconfigured))
                throw new InvalidOperationException("请先批量生成碰撞占地。");
            var missing = map.Regions.Where(r => r.CollisionCells.Count == 0 && r.CollisionMode != TileCollisionMode.None &&
                r.CollisionMode != TileCollisionMode.Manual && !IsEdgeFootprint(map, r)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("有自动规则未识别的占地：" + string.Join("、", missing.Select(r => r.Name)) + "。请定位复核；确认无碰撞的组可手工清空。");
            Tilemap grid = ReferenceGrid(map);
            var sources = TileOcclusionBuilder.FindSources(map);
            if (sources.Any(t => t.layoutGrid != grid.layoutGrid)) throw new InvalidOperationException("碰撞只支持单一 Grid。");
            Vector3 origin = map.transform.InverseTransformPoint(grid.CellToWorld(Vector3Int.zero));
            Vector3 right = map.transform.InverseTransformPoint(grid.CellToWorld(Vector3Int.right)) - origin;
            Vector3 up = map.transform.InverseTransformPoint(grid.CellToWorld(Vector3Int.up)) - origin;
            if (grid.orientation != Tilemap.Orientation.XY || grid.layoutGrid.cellLayout != GridLayout.CellLayout.Rectangle ||
                right.x <= 0 || up.y <= 0 || Mathf.Abs(right.x - up.y) > 0.0001f ||
                Mathf.Abs(right.y) + Mathf.Abs(right.z) + Mathf.Abs(up.x) + Mathf.Abs(up.z) > 0.0001f)
                throw new InvalidOperationException("碰撞/寻路需要地图局部坐标下未旋转的 XY 正方形网格。");
            var data = ScriptableObject.CreateInstance<TileCollisionData>();
            try
            {
                data.MinCell = new Vector2Int(sources.Min(t => t.cellBounds.xMin), sources.Min(t => t.cellBounds.yMin));
                data.Width = sources.Max(t => t.cellBounds.xMax) - data.MinCell.x;
                data.Height = sources.Max(t => t.cellBounds.yMax) - data.MinCell.y;
                data.GridOrigin = origin;
                data.CellSize = right.x;
                data.BlockedCells = Sort(map.Regions.Where(r => r.CollisionMode != TileCollisionMode.None).SelectMany(r => r.CollisionCells));
                data.Summary = $"{map.Regions.Count} 组，{data.BlockedCells.Count} 个碰撞格；物理和寻路共用同一占地。";
                data.Validate();
                return data;
            }
            catch { Object.DestroyImmediate(data); throw; }
        }

        public static List<RectInt> MergeRectangles(IEnumerable<Vector3Int> cells)
        {
            var pending = new HashSet<Vector3Int>(cells);
            var rectangles = new List<RectInt>();
            foreach (Vector3Int cell in Sort(cells))
            {
                if (!pending.Contains(cell)) continue;
                int width = 1, height = 1;
                while (pending.Contains(cell + new Vector3Int(width, 0, 0))) width++;
                bool FullRow(int y) { for (int x = 0; x < width; x++) if (!pending.Contains(cell + new Vector3Int(x, y, 0))) return false; return true; }
                while (FullRow(height)) height++;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pending.Remove(cell + new Vector3Int(x, y, 0));
                rectangles.Add(new RectInt(cell.x, cell.y, width, height));
            }
            return rectangles;
        }

        public static void PopulateCompanion(GameObject root, TileCollisionData data, int layer)
        {
            data.Validate();
            root.AddComponent<TileCollisionNavigationAuthoring>().Data = data;
            foreach (RectInt rect in MergeRectangles(data.BlockedCells))
            {
                var child = new GameObject($"Collision_{rect.x}_{rect.y}_{rect.width}x{rect.height}");
                child.layer = layer;
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = data.CellCorner(new Vector3Int(rect.x, rect.y, 0)) +
                    new Vector3(rect.width, rect.height, 0) * data.CellSize * 0.5f;
                child.AddComponent<BoxCollider>().size = new Vector3(rect.width * data.CellSize, rect.height * data.CellSize, data.Depth);
            }
            PopulateBoundary(root, data, layer);
        }

        public static void PopulateBoundary(GameObject root, TileCollisionData data, int layer)
        {
            data.Validate();
            Rect area = data.LocalBounds;
            float thickness = data.CellSize;
            // Outside the playable cells, with joined corners so diagonal
            // movement cannot find a gap. Navigation already treats outside as blocked.
            AddWall("Left", new Vector2(area.xMin - thickness * 0.5f, area.center.y), new Vector2(thickness, area.height + 2 * thickness));
            AddWall("Right", new Vector2(area.xMax + thickness * 0.5f, area.center.y), new Vector2(thickness, area.height + 2 * thickness));
            AddWall("Bottom", new Vector2(area.center.x, area.yMin - thickness * 0.5f), new Vector2(area.width, thickness));
            AddWall("Top", new Vector2(area.center.x, area.yMax + thickness * 0.5f), new Vector2(area.width, thickness));

            void AddWall(string side, Vector2 center, Vector2 size)
            {
                var child = new GameObject("Boundary_" + side) { layer = layer };
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(center.x, center.y, data.GridOrigin.z);
                child.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, data.Depth);
            }
        }

        public static GameObject Export(TileOcclusionMap map, string outputFolder)
        {
            TileCollisionData data = CreateData(map);
            // Unique directories make older exported maps and undo references safe.
            string parent = Path.GetDirectoryName(outputFolder).Replace('\\', '/');
            TileOcclusionBuilder.EnsureAssetFolder(parent);
            string folder = AssetDatabase.GenerateUniqueAssetPath(outputFolder);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
            { Object.DestroyImmediate(data); throw new InvalidOperationException("无法创建碰撞输出目录。"); }
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                AssetDatabase.CreateAsset(data, folder + "/CollisionData.asset");
                root = new GameObject(map.name + "_Physics");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                PopulateCompanion(root, data, map.gameObject.layer);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + root.name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("碰撞 Prefab 保存失败。");
                Undo.RecordObject(map, "Assign collision output");
                map.CollisionData = data;
                map.CollisionPrefab = prefab;
                map.CollisionNeedsRebuild = false;
                EditorUtility.SetDirty(map);
                PrefabUtility.RecordPrefabInstancePropertyModifications(map);
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                return prefab;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
