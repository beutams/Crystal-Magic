#if LEGACY_ROOM_DUNGEON_REFERENCE
using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.MapDemo;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class DungeonInteriorPlacementUtility
    {
        private const float DecorationVisualZ = 0.6f;

        private static readonly DungeonEdgeAnchor[] s_edgeAnchors =
        {
            DungeonEdgeAnchor.TopLeft,
            DungeonEdgeAnchor.Top,
            DungeonEdgeAnchor.TopRight,
            DungeonEdgeAnchor.Right,
            DungeonEdgeAnchor.BottomRight,
            DungeonEdgeAnchor.Bottom,
            DungeonEdgeAnchor.BottomLeft,
            DungeonEdgeAnchor.Left,
        };

        public static void AddDecorationSpawns(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout,
            DungeonThemeData theme,
            int seed)
        {
            if (sceneData == null || layout == null || theme == null)
                return;

            theme.EnsureValid();

            int[,] regionOwnerMap = BuildRegionOwnerMap(layout);
            IReadOnlyList<DungeonMakerRegion> regions = layout.Regions;
            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                DungeonMakerRegion region = regions[regionIndex];
                if (region == null || region.Kind is not (DungeonMakerRegionKind.Room or DungeonMakerRegionKind.AnteRoom))
                    continue;

                int styleId = region.VisualStyleId > 0 ? region.VisualStyleId : theme?.RootVisualStyleId ?? -1;
                DungeonVisualStyleData style = theme.GetVisualStyle(styleId);
                if (style == null)
                    continue;

                style.EnsureValid();
                List<DungeonInteriorProfileData> profiles = region.Kind == DungeonMakerRegionKind.Room
                    ? style.RoomProfiles
                    : style.AnteRoomProfiles;
                DungeonInteriorProfileData profile = ChooseProfile(profiles, layout, region, seed);
                if (profile == null)
                    continue;

                HashSet<Vector2Int> regionCells = BuildDisplayRegionCells(layout, region);
                if (regionCells.Count == 0)
                    continue;

                RectInt bounds = GetBounds(regionCells);
                HashSet<Vector2Int> protectedCells = BuildDoorwayClearance(layout, region, regionCells, regionOwnerMap);
                HashSet<Vector2Int> occupiedCells = new();
                System.Random random = new(unchecked(seed * 486187739 + region.Id * 16777619));
                Vector2Int visualCenter = FindVisualCenter(regionCells, protectedCells, bounds);

                PlaceMainDecoration(sceneData, layout, profile, regionCells, protectedCells, occupiedCells, visualCenter, random);
                PlaceSecondaryDecorations(sceneData, layout, profile, regionCells, protectedCells, occupiedCells, visualCenter, random);
                PlaceEdgeDecorations(sceneData, layout, profile, regionCells, protectedCells, occupiedCells, bounds, random);
            }
        }

        private static DungeonInteriorProfileData ChooseProfile(
            List<DungeonInteriorProfileData> profiles,
            DungeonMakerTunnelingResult layout,
            DungeonMakerRegion region,
            int seed)
        {
            if (profiles == null || profiles.Count == 0)
                return null;

            GetRegionSize(layout, region, out int width, out int height);
            List<DungeonInteriorProfileData> candidates = new();
            for (int i = 0; i < profiles.Count; i++)
            {
                DungeonInteriorProfileData profile = profiles[i];
                if (profile == null)
                    continue;

                profile.EnsureValid();
                if (width < profile.MinWidth
                    || height < profile.MinHeight)
                {
                    continue;
                }

                candidates.Add(profile);
            }

            return PickByWeight(candidates, unchecked(seed * 31 + region.Id * 101), static profile => profile.Weight);
        }

        private static void PlaceMainDecoration(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout,
            DungeonInteriorProfileData profile,
            HashSet<Vector2Int> regionCells,
            HashSet<Vector2Int> protectedCells,
            HashSet<Vector2Int> occupiedCells,
            Vector2Int visualCenter,
            System.Random random)
        {
            DungeonMainDecorationData decoration = PickByWeight(profile.MainDecorations, random, static entry => entry.Weight);
            if (decoration == null || string.IsNullOrWhiteSpace(decoration.PrefabName))
                return;

            RectInt footprint = GetCenteredFootprint(visualCenter, decoration.Width, decoration.Height);
            if (!TryOccupyFootprint(footprint, regionCells, protectedCells, occupiedCells))
                return;

            AddDecorationSpawn(sceneData, layout, footprint, decoration.PrefabName, 0f);
        }

        private static void PlaceSecondaryDecorations(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout,
            DungeonInteriorProfileData profile,
            HashSet<Vector2Int> regionCells,
            HashSet<Vector2Int> protectedCells,
            HashSet<Vector2Int> occupiedCells,
            Vector2Int visualCenter,
            System.Random random)
        {
            DungeonSecondaryDecorationData decoration = PickByWeight(profile.SecondaryDecorations, random, static entry => entry.Weight);
            if (decoration == null || string.IsNullOrWhiteSpace(decoration.PrefabName))
                return;

            int requestedPairs = random.Next(decoration.MinPairs, decoration.MaxPairs + 1);
            int placedPairs = 0;
            int attempts = Mathf.Max(1, requestedPairs * 6);
            for (int attempt = 0; attempt < attempts && placedPairs < requestedPairs; attempt++)
            {
                int distance = random.Next(decoration.MinDistance, decoration.MaxDistance + 1);
                Vector2Int leftCenter = new(visualCenter.x - distance, visualCenter.y);
                Vector2Int rightCenter = new(visualCenter.x + distance, visualCenter.y);
                RectInt leftFootprint = GetCenteredFootprint(leftCenter, decoration.Width, decoration.Height);
                RectInt rightFootprint = GetCenteredFootprint(rightCenter, decoration.Width, decoration.Height);

                if (FootprintsOverlap(leftFootprint, rightFootprint)
                    || !CanOccupyFootprint(leftFootprint, regionCells, protectedCells, occupiedCells)
                    || !CanOccupyFootprint(rightFootprint, regionCells, protectedCells, occupiedCells))
                {
                    continue;
                }

                OccupyFootprint(leftFootprint, occupiedCells);
                OccupyFootprint(rightFootprint, occupiedCells);
                AddDecorationSpawn(sceneData, layout, leftFootprint, decoration.PrefabName, 0f);
                AddDecorationSpawn(sceneData, layout, rightFootprint, decoration.PrefabName, 0f);
                placedPairs++;
            }
        }

        private static void PlaceEdgeDecorations(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout,
            DungeonInteriorProfileData profile,
            HashSet<Vector2Int> regionCells,
            HashSet<Vector2Int> protectedCells,
            HashSet<Vector2Int> occupiedCells,
            RectInt bounds,
            System.Random random)
        {
            DungeonEdgeDecorationData decoration = PickByWeight(profile.EdgeDecorations, random, static entry => entry.Weight);
            if (decoration == null || string.IsNullOrWhiteSpace(decoration.PrefabName))
                return;

            List<DungeonEdgeAnchor> anchors = new();
            for (int i = 0; i < s_edgeAnchors.Length; i++)
            {
                DungeonEdgeAnchor anchor = s_edgeAnchors[i];
                if ((decoration.AllowedAnchors & anchor) != 0)
                    anchors.Add(anchor);
            }

            for (int instanceIndex = 0; instanceIndex < decoration.MaxInstances && anchors.Count > 0; instanceIndex++)
            {
                int anchorIndex = random.Next(anchors.Count);
                DungeonEdgeAnchor anchor = anchors[anchorIndex];
                anchors.RemoveAt(anchorIndex);
                RectInt footprint = GetEdgeFootprint(bounds, decoration, anchor);
                if (!TryOccupyFootprint(footprint, regionCells, protectedCells, occupiedCells))
                    continue;

                AddDecorationSpawn(sceneData, layout, footprint, decoration.PrefabName, GetEdgeRotation(anchor));
            }
        }

        private static DungeonInteriorProfileData PickByWeight(
            IList<DungeonInteriorProfileData> values,
            int seed,
            Func<DungeonInteriorProfileData, int> getWeight)
        {
            if (values == null || values.Count == 0)
                return null;

            return PickByWeight(values, new System.Random(seed), getWeight);
        }

        private static T PickByWeight<T>(IList<T> values, System.Random random, Func<T, int> getWeight) where T : class
        {
            if (values == null || values.Count == 0)
                return null;

            int totalWeight = 0;
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value != null)
                    totalWeight += Mathf.Max(1, getWeight(value));
            }

            if (totalWeight <= 0)
                return null;

            int roll = random.Next(totalWeight);
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value == null)
                    continue;

                roll -= Mathf.Max(1, getWeight(value));
                if (roll < 0)
                    return value;
            }

            return null;
        }

        private static HashSet<Vector2Int> BuildDisplayRegionCells(DungeonMakerTunnelingResult layout, DungeonMakerRegion region)
        {
            HashSet<Vector2Int> cells = new();
            for (int i = 0; i < region.TileIndices.Length; i++)
            {
                int tileIndex = region.TileIndices[i];
                int sourceX = tileIndex / layout.SourceHeight;
                int sourceY = tileIndex % layout.SourceHeight;
                if (sourceX < 0 || sourceX >= layout.SourceWidth || sourceY < 0 || sourceY >= layout.SourceHeight)
                    continue;

                cells.Add(new Vector2Int(sourceY, sourceX));
            }

            return cells;
        }

        private static int[,] BuildRegionOwnerMap(DungeonMakerTunnelingResult layout)
        {
            int[,] map = new int[layout.DisplayWidth, layout.DisplayHeight];
            IReadOnlyList<DungeonMakerRegion> regions = layout.Regions;
            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                DungeonMakerRegion region = regions[regionIndex];
                if (region?.TileIndices == null)
                    continue;

                for (int tileIndexIndex = 0; tileIndexIndex < region.TileIndices.Length; tileIndexIndex++)
                {
                    int tileIndex = region.TileIndices[tileIndexIndex];
                    int sourceX = tileIndex / layout.SourceHeight;
                    int sourceY = tileIndex % layout.SourceHeight;
                    if (sourceX >= 0 && sourceX < layout.SourceWidth && sourceY >= 0 && sourceY < layout.SourceHeight)
                        map[sourceY, sourceX] = region.Id;
                }
            }

            return map;
        }

        private static HashSet<Vector2Int> BuildDoorwayClearance(
            DungeonMakerTunnelingResult layout,
            DungeonMakerRegion region,
            HashSet<Vector2Int> regionCells,
            int[,] regionOwnerMap)
        {
            HashSet<Vector2Int> result = new();
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (Vector2Int cell in regionCells)
            {
                for (int offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                {
                    Vector2Int neighbor = cell + offsets[offsetIndex];
                    if (neighbor.x < 0 || neighbor.x >= layout.DisplayWidth || neighbor.y < 0 || neighbor.y >= layout.DisplayHeight)
                        continue;

                    if (regionOwnerMap[neighbor.x, neighbor.y] == region.Id || !IsWalkable(layout.GetDisplayTile(neighbor.x, neighbor.y)))
                        continue;

                    result.Add(cell);
                }
            }

            return result;
        }

        private static Vector2Int FindVisualCenter(HashSet<Vector2Int> regionCells, HashSet<Vector2Int> protectedCells, RectInt bounds)
        {
            Vector2Int fallback = new(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
            Vector2Int best = fallback;
            int bestClearance = int.MinValue;
            int bestCenterDistance = int.MaxValue;
            foreach (Vector2Int candidate in regionCells)
            {
                if (protectedCells.Contains(candidate))
                    continue;

                int clearance = GetCellClearance(candidate, regionCells);
                int centerDistance = Mathf.Abs(candidate.x - fallback.x) + Mathf.Abs(candidate.y - fallback.y);
                if (clearance > bestClearance || clearance == bestClearance && centerDistance < bestCenterDistance)
                {
                    best = candidate;
                    bestClearance = clearance;
                    bestCenterDistance = centerDistance;
                }
            }

            return best;
        }

        private static int GetCellClearance(Vector2Int cell, HashSet<Vector2Int> regionCells)
        {
            for (int radius = 1; radius <= 64; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int y = radius - Mathf.Abs(x);
                    if (!regionCells.Contains(new Vector2Int(cell.x + x, cell.y + y))
                        || !regionCells.Contains(new Vector2Int(cell.x + x, cell.y - y)))
                    {
                        return radius - 1;
                    }
                }
            }

            return 64;
        }

        private static RectInt GetBounds(HashSet<Vector2Int> cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static void GetRegionSize(DungeonMakerTunnelingResult layout, DungeonMakerRegion region, out int width, out int height)
        {
            RectInt bounds = GetBounds(BuildDisplayRegionCells(layout, region));
            width = bounds.width;
            height = bounds.height;
        }

        private static RectInt GetCenteredFootprint(Vector2Int center, int width, int height)
        {
            return new RectInt(center.x - width / 2, center.y - height / 2, width, height);
        }

        private static RectInt GetEdgeFootprint(RectInt bounds, DungeonEdgeDecorationData decoration, DungeonEdgeAnchor anchor)
        {
            int anchorTargetX = bounds.x;
            int anchorTargetY = bounds.y;
            switch (anchor)
            {
                case DungeonEdgeAnchor.TopLeft:
                    anchorTargetY = bounds.yMax - decoration.AnchorHeight;
                    break;
                case DungeonEdgeAnchor.Top:
                    anchorTargetX = bounds.x + bounds.width / 2 - decoration.AnchorWidth / 2;
                    anchorTargetY = bounds.yMax - decoration.AnchorHeight;
                    break;
                case DungeonEdgeAnchor.TopRight:
                    anchorTargetX = bounds.xMax - decoration.AnchorWidth;
                    anchorTargetY = bounds.yMax - decoration.AnchorHeight;
                    break;
                case DungeonEdgeAnchor.Right:
                    anchorTargetX = bounds.xMax - decoration.AnchorWidth;
                    anchorTargetY = bounds.y + bounds.height / 2 - decoration.AnchorHeight / 2;
                    break;
                case DungeonEdgeAnchor.BottomRight:
                    anchorTargetX = bounds.xMax - decoration.AnchorWidth;
                    break;
                case DungeonEdgeAnchor.Bottom:
                    anchorTargetX = bounds.x + bounds.width / 2 - decoration.AnchorWidth / 2;
                    break;
                case DungeonEdgeAnchor.BottomLeft:
                    break;
                case DungeonEdgeAnchor.Left:
                    anchorTargetY = bounds.y + bounds.height / 2 - decoration.AnchorHeight / 2;
                    break;
            }

            return new RectInt(
                anchorTargetX - decoration.AnchorX,
                anchorTargetY - decoration.AnchorY,
                decoration.Width,
                decoration.Height);
        }

        private static bool TryOccupyFootprint(
            RectInt footprint,
            HashSet<Vector2Int> regionCells,
            HashSet<Vector2Int> protectedCells,
            HashSet<Vector2Int> occupiedCells)
        {
            if (!CanOccupyFootprint(footprint, regionCells, protectedCells, occupiedCells))
                return false;

            OccupyFootprint(footprint, occupiedCells);
            return true;
        }

        private static bool CanOccupyFootprint(
            RectInt footprint,
            HashSet<Vector2Int> regionCells,
            HashSet<Vector2Int> protectedCells,
            HashSet<Vector2Int> occupiedCells)
        {
            for (int y = footprint.yMin; y < footprint.yMax; y++)
            {
                for (int x = footprint.xMin; x < footprint.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (!regionCells.Contains(cell) || protectedCells.Contains(cell) || occupiedCells.Contains(cell))
                        return false;
                }
            }

            return true;
        }

        private static bool FootprintsOverlap(RectInt left, RectInt right)
        {
            return left.xMin < right.xMax
                && left.xMax > right.xMin
                && left.yMin < right.yMax
                && left.yMax > right.yMin;
        }

        private static void OccupyFootprint(RectInt footprint, HashSet<Vector2Int> occupiedCells)
        {
            for (int y = footprint.yMin; y < footprint.yMax; y++)
            {
                for (int x = footprint.xMin; x < footprint.xMax; x++)
                    occupiedCells.Add(new Vector2Int(x, y));
            }
        }

        private static void AddDecorationSpawn(
            RuntimeDungeonSceneData sceneData,
            DungeonMakerTunnelingResult layout,
            RectInt footprint,
            string prefabName,
            float rotationDegrees)
        {
            float cellSize = sceneData.CellWorldSize;
            sceneData.EnvironmentSpawns.Add(new RuntimeDungeonEnvironmentSpawnData
            {
                PrefabName = prefabName,
                WorldPosition = GetWorldPosition(footprint, layout.DisplayWidth, layout.DisplayHeight, cellSize),
                Size = new Vector3(footprint.width * cellSize, footprint.height * cellSize, 1f),
                RotationDegrees = rotationDegrees,
                ApplyCollider = true,
                IsDecoration = true,
            });
        }

        private static Vector3 GetWorldPosition(RectInt footprint, int displayWidth, int displayHeight, float cellSize)
        {
            float centerX = (footprint.x + footprint.width * 0.5f - displayWidth * 0.5f) * cellSize;
            float centerY = (footprint.y + footprint.height * 0.5f - displayHeight * 0.5f) * cellSize;
            return new Vector3(centerX, centerY, DecorationVisualZ);
        }

        private static float GetEdgeRotation(DungeonEdgeAnchor anchor)
        {
            return anchor switch
            {
                DungeonEdgeAnchor.TopLeft or DungeonEdgeAnchor.Top or DungeonEdgeAnchor.TopRight => 180f,
                DungeonEdgeAnchor.Left => 90f,
                DungeonEdgeAnchor.Right => -90f,
                _ => 0f,
            };
        }

        private static bool IsWalkable(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN
                or DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IT_OPEN
                or DungeonMakerSquareData.IA_OPEN
                or DungeonMakerSquareData.H_DOOR
                or DungeonMakerSquareData.V_DOOR
                or DungeonMakerSquareData.MOB1
                or DungeonMakerSquareData.MOB2
                or DungeonMakerSquareData.MOB3
                or DungeonMakerSquareData.TREAS1
                or DungeonMakerSquareData.TREAS2
                or DungeonMakerSquareData.TREAS3;
        }
    }
}

#endif
