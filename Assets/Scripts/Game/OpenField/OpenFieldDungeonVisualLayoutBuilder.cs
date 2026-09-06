using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;
using Random = System.Random;

namespace CrystalMagic.Game.OpenField
{
    /// <summary>
    /// The target Tilemap for a generated RuleTile. These layers are intentionally kept
    /// separate so that one cell may have a ground tile and a cliff transition at once.
    /// </summary>
    public enum OpenFieldRuleTileLayer : byte
    {
        Void,
        Ground,
        Decoration,
        Obstacle,
        Boundary,
    }

    /// <summary>
    /// An asset-independent instruction for a future temporary RuleTile Tilemap.
    /// </summary>
    public sealed class OpenFieldRuleTilePlacement
    {
        public OpenFieldRuleTilePlacement(
            OpenFieldRuleTileLayer layer,
            OpenFieldRuleTileReferenceData ruleTile,
            Vector2Int cell)
        {
            Layer = layer;
            RuleTile = ruleTile;
            Cell = cell;
        }

        public OpenFieldRuleTileLayer Layer { get; }
        public OpenFieldRuleTileReferenceData RuleTile { get; }
        public Vector2Int Cell { get; }
    }

    public sealed class OpenFieldObstacleVisualSpritePlacement
    {
        public OpenFieldObstacleVisualSpritePlacement(
            OpenFieldSpriteReferenceData sprite,
            Vector2Int localCell,
            int layerIndex,
            bool useObstacleCenter)
        {
            Sprite = sprite;
            LocalCell = localCell;
            LayerIndex = layerIndex;
            UseObstacleCenter = useObstacleCenter;
        }

        public OpenFieldSpriteReferenceData Sprite { get; }
        public Vector2Int LocalCell { get; }
        public int LayerIndex { get; }
        public bool UseObstacleCenter { get; }
    }

    /// <summary>
    /// An obstacle footprint after the configured rotation and flip have been applied.
    /// OccupiedCells contains all visual footprint cells; CollisionCells contains only
    /// the cells that need an ECS collider.
    /// </summary>
    public sealed class OpenFieldObstaclePlacement
    {
        public OpenFieldObstaclePlacement(
            int groundStyleIndex,
            int obstacleIndex,
            OpenFieldObstacleData obstacle,
            Vector2Int origin,
            int rotationQuarterTurns,
            bool flippedX,
            IReadOnlyList<Vector2Int> occupiedCells,
            IReadOnlyList<Vector2Int> collisionCells,
            IReadOnlyList<OpenFieldObstacleVisualSpritePlacement> visualSprites)
        {
            GroundStyleIndex = groundStyleIndex;
            ObstacleIndex = obstacleIndex;
            Obstacle = obstacle;
            Origin = origin;
            RotationQuarterTurns = rotationQuarterTurns;
            FlippedX = flippedX;
            OccupiedCells = occupiedCells;
            CollisionCells = collisionCells;
            VisualSprites = visualSprites;
        }

        public int GroundStyleIndex { get; }
        public int ObstacleIndex { get; }
        public OpenFieldObstacleData Obstacle { get; }
        public Vector2Int Origin { get; }
        public int RotationQuarterTurns { get; }
        public bool FlippedX { get; }
        public Vector2 VisualSortAnchor => Obstacle.VisualSortAnchor.ToVector2();
        public float MinimumSpacing => Obstacle.MinimumSpacing;
        public IReadOnlyList<Vector2Int> OccupiedCells { get; }
        public IReadOnlyList<Vector2Int> CollisionCells { get; }
        public IReadOnlyList<OpenFieldObstacleVisualSpritePlacement> VisualSprites { get; }
    }

    /// <summary>
    /// Pure data output shared by the scene-data builder and edit-mode tests.
    /// </summary>
    public sealed class OpenFieldDungeonVisualLayout
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
        };

        private readonly OpenFieldDungeonLayout _layout;
        private readonly int[] _groundStyleIndices;
        private readonly HashSet<Vector2Int> _protectedCells;

        internal OpenFieldDungeonVisualLayout(
            OpenFieldDungeonLayout layout,
            int[] groundStyleIndices,
            HashSet<Vector2Int> protectedCells,
            List<OpenFieldRuleTilePlacement> ruleTilePlacements,
            List<OpenFieldObstaclePlacement> obstacles)
        {
            _layout = layout;
            _groundStyleIndices = groundStyleIndices;
            _protectedCells = protectedCells;
            RuleTilePlacements = ruleTilePlacements;
            Obstacles = obstacles;
        }

        public IReadOnlyList<OpenFieldRuleTilePlacement> RuleTilePlacements { get; }
        public IReadOnlyList<OpenFieldObstaclePlacement> Obstacles { get; }

        public int GetGroundStyleIndex(int x, int y)
        {
            if (!_layout.IsInside(x, y) || _layout.GetTerrainCell(x, y) != OpenFieldTerrainCell.Ground)
                return -1;

            return _groundStyleIndices[_layout.GetIndex(x, y)];
        }

        public bool IsStyleInterior(Vector2Int cell, int styleIndex)
        {
            if (styleIndex < 0 || GetGroundStyleIndex(cell.x, cell.y) != styleIndex)
                return false;

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighbour = cell + direction;
                if (GetGroundStyleIndex(neighbour.x, neighbour.y) != styleIndex)
                    return false;
            }

            return true;
        }

        public bool IsValidObstacleCollisionCell(Vector2Int cell)
        {
            if (GetGroundStyleIndex(cell.x, cell.y) < 0 || _protectedCells.Contains(cell))
                return false;

            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                for (int deltaX = -1; deltaX <= 1; deltaX++)
                {
                    if (deltaX == 0 && deltaY == 0)
                        continue;

                    int neighbourX = cell.x + deltaX;
                    int neighbourY = cell.y + deltaY;
                    if (!_layout.IsInside(neighbourX, neighbourY) ||
                        _layout.GetTerrainCell(neighbourX, neighbourY) != OpenFieldTerrainCell.Ground)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Builds open-field visual decisions without resolving Unity assets or constructing
    /// any GameObjects. Every pseudo-random choice comes from the semantic layout seed.
    /// </summary>
    public static class OpenFieldDungeonVisualLayoutBuilder
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
        };

        public static OpenFieldDungeonVisualLayout Build(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualData visual,
            IReadOnlyCollection<Vector2Int> protectedCells)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (visual == null)
                throw new ArgumentNullException(nameof(visual));

            visual.EnsureValid();
            if (visual.GroundStyles.Count == 0)
                throw new InvalidOperationException("Open-field visual data needs at least one Ground Style.");

            HashSet<Vector2Int> protectedCellSet = new();
            if (protectedCells != null)
            {
                foreach (Vector2Int cell in protectedCells)
                {
                    if (layout.IsInside(cell.x, cell.y))
                        protectedCellSet.Add(cell);
                }
            }

            int[] groundStyleIndices = AssignGroundStyles(layout, visual);
            List<OpenFieldRuleTilePlacement> placements = CreateTerrainPlacements(layout, visual, groundStyleIndices);
            OpenFieldDungeonVisualLayout result = new(
                layout,
                groundStyleIndices,
                protectedCellSet,
                placements,
                new List<OpenFieldObstaclePlacement>());

            AddDecorationPlacements(layout, visual, result, placements);
            List<OpenFieldObstaclePlacement> obstacles = CreateObstaclePlacements(layout, visual, result);
            return new OpenFieldDungeonVisualLayout(layout, groundStyleIndices, protectedCellSet, placements, obstacles);
        }

        private static int[] AssignGroundStyles(OpenFieldDungeonLayout layout, OpenFieldDungeonVisualData visual)
        {
            int[] styleIndices = new int[layout.CellCount];
            for (int i = 0; i < styleIndices.Length; i++)
                styleIndices[i] = -1;

            List<List<Vector2Int>> regions = FindGroundRegions(layout);
            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                List<Vector2Int> region = regions[regionIndex];
                Random random = CreateRandom(layout.Seed, 101 + regionIndex);
                List<Vector2Int> shuffledCells = new(region);
                Shuffle(shuffledCells, random);

                int seedCount = Math.Min(
                    region.Count,
                    Math.Max(1, DivideRoundUp(region.Count, visual.GroundCellsPerStyleSeed)));
                List<int> styleCycle = new();
                for (int styleIndex = 0; styleIndex < visual.GroundStyles.Count; styleIndex++)
                    styleCycle.Add(styleIndex);
                Shuffle(styleCycle, random);

                List<StyleSeed> seeds = new();
                for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
                {
                    Vector2Int cell = shuffledCells[seedIndex];
                    int styleIndex = styleCycle[seedIndex % styleCycle.Count];
                    styleIndices[layout.GetIndex(cell.x, cell.y)] = styleIndex;
                    seeds.Add(new StyleSeed(styleIndex, cell));
                }

                int remainingCells = region.Count - seedCount;
                while (remainingCells > 0)
                {
                    bool progressed = false;
                    foreach (StyleSeed seed in seeds)
                    {
                        List<Vector2Int> candidates = FindUnclaimedCardinalNeighbours(layout, styleIndices, seed.Frontier);
                        if (candidates.Count == 0)
                            continue;

                        Vector2Int selected = candidates[random.Next(candidates.Count)];
                        styleIndices[layout.GetIndex(selected.x, selected.y)] = seed.StyleIndex;
                        seed.Frontier.Add(selected);
                        remainingCells--;
                        progressed = true;
                    }

                    if (!progressed)
                        throw new InvalidOperationException("A connected Ground region could not be assigned a visual style.");
                }

                RemoveUnsupportedStyleTips(layout, styleIndices, region);
            }

            return styleIndices;
        }

        private static List<List<Vector2Int>> FindGroundRegions(OpenFieldDungeonLayout layout)
        {
            bool[] visited = new bool[layout.CellCount];
            List<List<Vector2Int>> regions = new();
            Queue<Vector2Int> queue = new();

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    int index = layout.GetIndex(x, y);
                    if (visited[index] || layout.GetTerrainCell(x, y) != OpenFieldTerrainCell.Ground)
                        continue;

                    List<Vector2Int> region = new();
                    visited[index] = true;
                    queue.Enqueue(new Vector2Int(x, y));
                    while (queue.Count > 0)
                    {
                        Vector2Int cell = queue.Dequeue();
                        region.Add(cell);
                        foreach (Vector2Int direction in CardinalDirections)
                        {
                            Vector2Int neighbour = cell + direction;
                            if (!layout.IsInside(neighbour.x, neighbour.y))
                                continue;

                            int neighbourIndex = layout.GetIndex(neighbour.x, neighbour.y);
                            if (visited[neighbourIndex] ||
                                layout.GetTerrainCell(neighbour.x, neighbour.y) != OpenFieldTerrainCell.Ground)
                            {
                                continue;
                            }

                            visited[neighbourIndex] = true;
                            queue.Enqueue(neighbour);
                        }
                    }

                    regions.Add(region);
                }
            }

            return regions;
        }

        private static List<Vector2Int> FindUnclaimedCardinalNeighbours(
            OpenFieldDungeonLayout layout,
            int[] styleIndices,
            IReadOnlyList<Vector2Int> frontier)
        {
            List<Vector2Int> candidates = new();
            HashSet<Vector2Int> added = new();
            foreach (Vector2Int cell in frontier)
            {
                foreach (Vector2Int direction in CardinalDirections)
                {
                    Vector2Int neighbour = cell + direction;
                    if (!layout.IsInside(neighbour.x, neighbour.y) ||
                        layout.GetTerrainCell(neighbour.x, neighbour.y) != OpenFieldTerrainCell.Ground ||
                        styleIndices[layout.GetIndex(neighbour.x, neighbour.y)] >= 0 ||
                        !added.Add(neighbour))
                    {
                        continue;
                    }

                    candidates.Add(neighbour);
                }
            }

            return candidates;
        }

        private static void RemoveUnsupportedStyleTips(
            OpenFieldDungeonLayout layout,
            int[] styleIndices,
            IReadOnlyList<Vector2Int> region)
        {
            int[] snapshot = new int[styleIndices.Length];
            Array.Copy(styleIndices, snapshot, styleIndices.Length);
            List<KeyValuePair<Vector2Int, int>> replacements = new();
            foreach (Vector2Int cell in region)
            {
                int ownStyle = snapshot[layout.GetIndex(cell.x, cell.y)];
                if (HasTwoByTwoSupport(layout, snapshot, cell, ownStyle))
                    continue;

                int replacementStyle = FindMostCommonNeighbourStyle(layout, snapshot, cell, ownStyle);
                if (replacementStyle >= 0 && replacementStyle != ownStyle)
                    replacements.Add(new KeyValuePair<Vector2Int, int>(cell, replacementStyle));
            }

            foreach (KeyValuePair<Vector2Int, int> replacement in replacements)
                styleIndices[layout.GetIndex(replacement.Key.x, replacement.Key.y)] = replacement.Value;
        }

        private static bool HasTwoByTwoSupport(
            OpenFieldDungeonLayout layout,
            int[] styles,
            Vector2Int cell,
            int styleIndex)
        {
            for (int offsetY = -1; offsetY <= 0; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 0; offsetX++)
                {
                    bool supportsCell = true;
                    for (int squareY = 0; squareY < 2 && supportsCell; squareY++)
                    {
                        for (int squareX = 0; squareX < 2; squareX++)
                        {
                            int x = cell.x + offsetX + squareX;
                            int y = cell.y + offsetY + squareY;
                            if (!layout.IsInside(x, y) || styles[layout.GetIndex(x, y)] != styleIndex)
                            {
                                supportsCell = false;
                                break;
                            }
                        }
                    }

                    if (supportsCell)
                        return true;
                }
            }

            return false;
        }

        private static int FindMostCommonNeighbourStyle(
            OpenFieldDungeonLayout layout,
            int[] styles,
            Vector2Int cell,
            int ownStyle)
        {
            Dictionary<int, int> counts = new();
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighbour = cell + direction;
                if (!layout.IsInside(neighbour.x, neighbour.y))
                    continue;

                int style = styles[layout.GetIndex(neighbour.x, neighbour.y)];
                if (style < 0 || style == ownStyle)
                    continue;

                counts.TryGetValue(style, out int count);
                counts[style] = count + 1;
            }

            int selectedStyle = -1;
            int selectedCount = 0;
            foreach (KeyValuePair<int, int> entry in counts)
            {
                if (entry.Value > selectedCount ||
                    (entry.Value == selectedCount && (selectedStyle < 0 || entry.Key < selectedStyle)))
                {
                    selectedStyle = entry.Key;
                    selectedCount = entry.Value;
                }
            }

            return selectedStyle;
        }

        private static List<OpenFieldRuleTilePlacement> CreateTerrainPlacements(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualData visual,
            int[] groundStyleIndices)
        {
            List<OpenFieldRuleTilePlacement> placements = new();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    Vector2Int cell = new(x, y);
                    OpenFieldTerrainCell terrain = layout.GetTerrainCell(x, y);
                    Vector2Int frontCell = cell + Vector2Int.down;
                    bool hasFrontCell = layout.IsInside(frontCell.x, frontCell.y);
                    OpenFieldTerrainCell frontTerrain = hasFrontCell
                        ? layout.GetTerrainCell(frontCell.x, frontCell.y)
                        : OpenFieldTerrainCell.Void;

                    switch (terrain)
                    {
                        case OpenFieldTerrainCell.Ground:
                        {
                            int styleIndex = groundStyleIndices[layout.GetIndex(x, y)];
                            placements.Add(new OpenFieldRuleTilePlacement(
                                OpenFieldRuleTileLayer.Ground,
                                visual.GroundStyles[styleIndex].BaseRuleTile,
                                cell));
                            break;
                        }

                        case OpenFieldTerrainCell.Void:
                        {
                            bool exposedFront = !hasFrontCell || frontTerrain != OpenFieldTerrainCell.Void;
                            placements.Add(new OpenFieldRuleTilePlacement(
                                OpenFieldRuleTileLayer.Void,
                                exposedFront ? visual.VoidVisual.WallRuleTile : visual.VoidVisual.AbyssRuleTile,
                                cell));
                            if (hasFrontCell && frontTerrain == OpenFieldTerrainCell.Ground)
                            {
                                placements.Add(new OpenFieldRuleTilePlacement(
                                    OpenFieldRuleTileLayer.Void,
                                    visual.VoidVisual.TransitionRuleTile,
                                    frontCell));
                            }

                            break;
                        }

                        case OpenFieldTerrainCell.Obstacle:
                        {
                            int height = layout.GetHeightSteps(x, y);
                            Vector2Int topCell = cell + Vector2Int.up * height;
                            bool exposedFront = !hasFrontCell ||
                                                frontTerrain != OpenFieldTerrainCell.Obstacle ||
                                                layout.GetHeightSteps(frontCell.x, frontCell.y) < height;
                            placements.Add(new OpenFieldRuleTilePlacement(
                                OpenFieldRuleTileLayer.Obstacle,
                                exposedFront ? visual.ObstacleVisual.WallRuleTile : visual.ObstacleVisual.TopRuleTile,
                                topCell));
                            if (exposedFront)
                            {
                                for (int step = 0; step < height; step++)
                                {
                                    placements.Add(new OpenFieldRuleTilePlacement(
                                        OpenFieldRuleTileLayer.Obstacle,
                                        visual.ObstacleVisual.TransitionRuleTile,
                                        cell + Vector2Int.up * step));
                                }
                            }

                            break;
                        }

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            AddBoundaryPlacements(layout, visual.BoundaryRuleTile, placements);
            return placements;
        }

        private static void AddBoundaryPlacements(
            OpenFieldDungeonLayout layout,
            OpenFieldRuleTileReferenceData boundaryRuleTile,
            List<OpenFieldRuleTilePlacement> placements)
        {
            for (int x = -1; x <= layout.Width; x++)
            {
                placements.Add(new OpenFieldRuleTilePlacement(
                    OpenFieldRuleTileLayer.Boundary,
                    boundaryRuleTile,
                    new Vector2Int(x, -1)));
                placements.Add(new OpenFieldRuleTilePlacement(
                    OpenFieldRuleTileLayer.Boundary,
                    boundaryRuleTile,
                    new Vector2Int(x, layout.Height)));
            }

            for (int y = 0; y < layout.Height; y++)
            {
                placements.Add(new OpenFieldRuleTilePlacement(
                    OpenFieldRuleTileLayer.Boundary,
                    boundaryRuleTile,
                    new Vector2Int(-1, y)));
                placements.Add(new OpenFieldRuleTilePlacement(
                    OpenFieldRuleTileLayer.Boundary,
                    boundaryRuleTile,
                    new Vector2Int(layout.Width, y)));
            }
        }

        private static void AddDecorationPlacements(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualData visual,
            OpenFieldDungeonVisualLayout visualLayout,
            List<OpenFieldRuleTilePlacement> placements)
        {
            HashSet<Vector2Int> reservedCells = new();
            for (int styleIndex = 0; styleIndex < visual.GroundStyles.Count; styleIndex++)
            {
                OpenFieldGroundStyleData style = visual.GroundStyles[styleIndex];
                List<Vector2Int> styleCells = GetStyleCells(layout, visualLayout, styleIndex, false);
                List<Vector2Int> interiorCells = GetStyleCells(layout, visualLayout, styleIndex, true);
                if (styleCells.Count == 0 || interiorCells.Count == 0)
                    continue;

                for (int decorationIndex = 0; decorationIndex < style.Decorations.Count; decorationIndex++)
                {
                    OpenFieldDecorationData decoration = style.Decorations[decorationIndex];
                    Random random = CreateRandom(layout.Seed, 1009 + styleIndex * 97 + decorationIndex);
                    int seedCount = Math.Min(
                        interiorCells.Count,
                        Math.Max(1, (int)Math.Ceiling(styleCells.Count / (Math.PI * decoration.Radius * decoration.Radius))));

                    for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
                    {
                        List<Vector2Int> availableCenters = GetUnreservedCells(interiorCells, reservedCells);
                        if (availableCenters.Count == 0)
                            break;

                        Vector2Int center = availableCenters[random.Next(availableCenters.Count)];
                        List<Vector2Int> cluster = GrowDecorationCluster(
                            center,
                            decoration.MaximumSpread,
                            visualLayout,
                            styleIndex,
                            reservedCells,
                            random);
                        if (decoration.MaximumSpread > 0)
                            RemoveUnsupportedDecorationCells(cluster);

                        foreach (Vector2Int cell in cluster)
                        {
                            reservedCells.Add(cell);
                            placements.Add(new OpenFieldRuleTilePlacement(
                                OpenFieldRuleTileLayer.Decoration,
                                decoration.RuleTile,
                                cell));
                        }
                    }
                }
            }
        }

        private static List<Vector2Int> GetStyleCells(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualLayout visualLayout,
            int styleIndex,
            bool onlyInterior)
        {
            List<Vector2Int> cells = new();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (visualLayout.GetGroundStyleIndex(x, y) != styleIndex ||
                        (onlyInterior && !visualLayout.IsStyleInterior(cell, styleIndex)))
                    {
                        continue;
                    }

                    cells.Add(cell);
                }
            }

            return cells;
        }

        private static List<Vector2Int> GetUnreservedCells(
            IReadOnlyList<Vector2Int> cells,
            HashSet<Vector2Int> reservedCells)
        {
            List<Vector2Int> available = new();
            foreach (Vector2Int cell in cells)
            {
                if (!reservedCells.Contains(cell))
                    available.Add(cell);
            }

            return available;
        }

        private static List<Vector2Int> GrowDecorationCluster(
            Vector2Int center,
            int maximumSpread,
            OpenFieldDungeonVisualLayout visualLayout,
            int styleIndex,
            HashSet<Vector2Int> reservedCells,
            Random random)
        {
            List<Vector2Int> cluster = new() { center };
            HashSet<Vector2Int> clusterSet = new() { center };
            for (int step = 0; step < maximumSpread; step++)
            {
                List<Vector2Int> candidates = FindDecorationCandidates(
                    center,
                    cluster,
                    clusterSet,
                    visualLayout,
                    styleIndex,
                    reservedCells);
                if (candidates.Count == 0)
                    break;

                Vector2Int selected = candidates[random.Next(candidates.Count)];
                cluster.Add(selected);
                clusterSet.Add(selected);
            }

            return cluster;
        }

        private static List<Vector2Int> FindDecorationCandidates(
            Vector2Int center,
            IReadOnlyList<Vector2Int> cluster,
            HashSet<Vector2Int> clusterSet,
            OpenFieldDungeonVisualLayout visualLayout,
            int styleIndex,
            HashSet<Vector2Int> reservedCells)
        {
            List<Vector2Int> allCandidates = new();
            HashSet<Vector2Int> added = new();
            int bestSupport = int.MinValue;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int source in cluster)
            {
                foreach (Vector2Int direction in CardinalDirections)
                {
                    Vector2Int candidate = source + direction;
                    if (clusterSet.Contains(candidate) || reservedCells.Contains(candidate) ||
                        !visualLayout.IsStyleInterior(candidate, styleIndex) || !added.Add(candidate))
                    {
                        continue;
                    }

                    int support = CountCompletedTwoByTwoSquares(candidate, clusterSet);
                    int distance = Mathf.Abs(candidate.x - center.x) + Mathf.Abs(candidate.y - center.y);
                    if (support > bestSupport || (support == bestSupport && distance < bestDistance))
                    {
                        allCandidates.Clear();
                        bestSupport = support;
                        bestDistance = distance;
                    }

                    if (support == bestSupport && distance == bestDistance)
                        allCandidates.Add(candidate);
                }
            }

            return allCandidates;
        }

        private static int CountCompletedTwoByTwoSquares(Vector2Int candidate, HashSet<Vector2Int> cluster)
        {
            int count = 0;
            for (int offsetY = -1; offsetY <= 0; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 0; offsetX++)
                {
                    bool complete = true;
                    for (int squareY = 0; squareY < 2 && complete; squareY++)
                    {
                        for (int squareX = 0; squareX < 2; squareX++)
                        {
                            Vector2Int cell = new(candidate.x + offsetX + squareX, candidate.y + offsetY + squareY);
                            if (cell != candidate && !cluster.Contains(cell))
                            {
                                complete = false;
                                break;
                            }
                        }
                    }

                    if (complete)
                        count++;
                }
            }

            return count;
        }

        private static void RemoveUnsupportedDecorationCells(List<Vector2Int> cluster)
        {
            HashSet<Vector2Int> snapshot = new(cluster);
            List<Vector2Int> kept = new();
            foreach (Vector2Int cell in cluster)
            {
                if (HasTwoByTwoSupport(snapshot, cell))
                    kept.Add(cell);
            }

            cluster.Clear();
            cluster.AddRange(kept);
        }

        private static bool HasTwoByTwoSupport(HashSet<Vector2Int> cells, Vector2Int cell)
        {
            for (int offsetY = -1; offsetY <= 0; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 0; offsetX++)
                {
                    bool supported = true;
                    for (int squareY = 0; squareY < 2 && supported; squareY++)
                    {
                        for (int squareX = 0; squareX < 2; squareX++)
                        {
                            if (!cells.Contains(new Vector2Int(cell.x + offsetX + squareX, cell.y + offsetY + squareY)))
                            {
                                supported = false;
                                break;
                            }
                        }
                    }

                    if (supported)
                        return true;
                }
            }

            return false;
        }

        private static List<OpenFieldObstaclePlacement> CreateObstaclePlacements(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualData visual,
            OpenFieldDungeonVisualLayout visualLayout)
        {
            List<OpenFieldObstaclePlacement> placements = new();
            HashSet<Vector2Int> occupiedCells = new();
            HashSet<Vector2Int> collisionCells = new();
            for (int styleIndex = 0; styleIndex < visual.GroundStyles.Count; styleIndex++)
            {
                List<Vector2Int> anchors = GetStyleCells(layout, visualLayout, styleIndex, false);
                if (anchors.Count == 0)
                    continue;

                List<ObstacleDefinitionState> definitions = new();
                OpenFieldGroundStyleData style = visual.GroundStyles[styleIndex];
                for (int obstacleIndex = 0; obstacleIndex < style.Obstacles.Count; obstacleIndex++)
                {
                    OpenFieldObstacleData obstacle = style.Obstacles[obstacleIndex];
                    if (obstacle.MaximumCount > 0)
                        definitions.Add(new ObstacleDefinitionState(obstacleIndex, obstacle));
                }

                if (definitions.Count == 0)
                    continue;

                Random random = CreateRandom(layout.Seed, 3001 + styleIndex);
                int remainingPlacementCount = 0;
                foreach (ObstacleDefinitionState definition in definitions)
                    remainingPlacementCount += definition.Obstacle.MaximumCount;

                int attemptsRemaining = Math.Max(anchors.Count * Math.Max(1, remainingPlacementCount), 32);
                while (remainingPlacementCount > 0 && attemptsRemaining-- > 0)
                {
                    ObstacleDefinitionState definition = PickWeightedDefinition(definitions, random);
                    if (definition == null)
                        break;

                    int rotationQuarterTurns = definition.Obstacle.AllowRotation ? random.Next(4) : 0;
                    bool flippedX = definition.Obstacle.AllowFlipX && random.Next(2) == 1;
                    Vector2Int origin = anchors[random.Next(anchors.Count)];
                    List<ObstacleMaskCell> mask = TransformObstacleMask(definition.Obstacle, rotationQuarterTurns, flippedX);
                    if (!CanPlaceObstacle(
                            layout,
                            visualLayout,
                            styleIndex,
                            definition.Obstacle,
                            origin,
                            mask,
                            occupiedCells,
                            collisionCells,
                            placements))
                    {
                        continue;
                    }

                    List<Vector2Int> footprint = new();
                    List<Vector2Int> obstacleCollisionCells = new();
                    foreach (ObstacleMaskCell maskCell in mask)
                    {
                        Vector2Int cell = origin + maskCell.LocalCell;
                        footprint.Add(cell);
                        occupiedCells.Add(cell);
                        if (!maskCell.IsCollision)
                            continue;

                        obstacleCollisionCells.Add(cell);
                        collisionCells.Add(cell);
                    }

                    List<OpenFieldObstacleVisualSpritePlacement> visualSprites = TransformObstacleSprites(
                        definition.Obstacle,
                        rotationQuarterTurns,
                        flippedX);

                    placements.Add(new OpenFieldObstaclePlacement(
                        styleIndex,
                        definition.ObstacleIndex,
                        definition.Obstacle,
                        origin,
                        rotationQuarterTurns,
                        flippedX,
                        footprint,
                        obstacleCollisionCells,
                        visualSprites));
                    definition.PlacedCount++;
                    remainingPlacementCount--;
                }
            }

            return placements;
        }

        private static ObstacleDefinitionState PickWeightedDefinition(
            IReadOnlyList<ObstacleDefinitionState> definitions,
            Random random)
        {
            int totalWeight = 0;
            foreach (ObstacleDefinitionState definition in definitions)
            {
                if (definition.PlacedCount < definition.Obstacle.MaximumCount)
                    totalWeight += definition.Obstacle.Weight;
            }

            if (totalWeight <= 0)
                return null;

            int selectedWeight = random.Next(totalWeight);
            foreach (ObstacleDefinitionState definition in definitions)
            {
                if (definition.PlacedCount >= definition.Obstacle.MaximumCount)
                    continue;

                if (selectedWeight < definition.Obstacle.Weight)
                    return definition;
                selectedWeight -= definition.Obstacle.Weight;
            }

            return null;
        }

        private static List<ObstacleMaskCell> TransformObstacleMask(
            OpenFieldObstacleData obstacle,
            int rotationQuarterTurns,
            bool flippedX)
        {
            List<ObstacleMaskCell> cells = new();
            int turns = ((rotationQuarterTurns % 4) + 4) % 4;
            for (int y = 0; y < obstacle.FootprintHeight; y++)
            {
                for (int x = 0; x < obstacle.FootprintWidth; x++)
                {
                    int sourceX = x;
                    Vector2Int local = TransformObstacleCell(
                        x,
                        y,
                        obstacle.FootprintWidth,
                        obstacle.FootprintHeight,
                        turns,
                        flippedX);
                    int sourceIndex = y * obstacle.FootprintWidth + sourceX;
                    cells.Add(new ObstacleMaskCell(local, obstacle.CollisionMask[sourceIndex]));
                }
            }

            return cells;
        }

        private static List<OpenFieldObstacleVisualSpritePlacement> TransformObstacleSprites(
            OpenFieldObstacleData obstacle,
            int rotationQuarterTurns,
            bool flippedX)
        {
            List<OpenFieldObstacleVisualSpritePlacement> results = new();
            if (obstacle.SpriteLayers == null)
                return results;

            int turns = ((rotationQuarterTurns % 4) + 4) % 4;
            for (int layerIndex = 0; layerIndex < obstacle.SpriteLayers.Count; layerIndex++)
            {
                OpenFieldObstacleSpriteLayerData layer = obstacle.SpriteLayers[layerIndex];
                if (layer?.Cells == null)
                    continue;

                foreach (OpenFieldObstacleSpriteCellData spriteCell in layer.Cells)
                {
                    if (spriteCell?.Sprite == null || string.IsNullOrWhiteSpace(spriteCell.Sprite.AssetPath))
                        continue;
                    if (spriteCell.X < 0 || spriteCell.X >= obstacle.FootprintWidth ||
                        spriteCell.Y < 0 || spriteCell.Y >= obstacle.FootprintHeight)
                    {
                        continue;
                    }

                    Vector2Int localCell = TransformObstacleCell(
                        spriteCell.X,
                        spriteCell.Y,
                        obstacle.FootprintWidth,
                        obstacle.FootprintHeight,
                        turns,
                        flippedX);
                    results.Add(new OpenFieldObstacleVisualSpritePlacement(
                        spriteCell.Sprite,
                        localCell,
                        layerIndex,
                        spriteCell.UseObstacleCenter));
                }
            }

            return results;
        }

        private static Vector2Int TransformObstacleCell(
            int x,
            int y,
            int width,
            int height,
            int turns,
            bool flippedX)
        {
            int transformedX = flippedX ? width - 1 - x : x;
            return turns switch
            {
                0 => new Vector2Int(transformedX, y),
                1 => new Vector2Int(height - 1 - y, transformedX),
                2 => new Vector2Int(width - 1 - transformedX, height - 1 - y),
                3 => new Vector2Int(y, width - 1 - transformedX),
                _ => throw new ArgumentOutOfRangeException(nameof(turns)),
            };
        }

        private static bool CanPlaceObstacle(
            OpenFieldDungeonLayout layout,
            OpenFieldDungeonVisualLayout visualLayout,
            int styleIndex,
            OpenFieldObstacleData obstacle,
            Vector2Int origin,
            IReadOnlyList<ObstacleMaskCell> mask,
            HashSet<Vector2Int> occupiedCells,
            HashSet<Vector2Int> collisionCells,
            IReadOnlyList<OpenFieldObstaclePlacement> placements)
        {
            foreach (ObstacleMaskCell maskCell in mask)
            {
                Vector2Int cell = origin + maskCell.LocalCell;
                if (!layout.IsInside(cell.x, cell.y) ||
                    visualLayout.GetGroundStyleIndex(cell.x, cell.y) != styleIndex ||
                    occupiedCells.Contains(cell))
                {
                    return false;
                }

                if (maskCell.IsCollision &&
                    (!visualLayout.IsValidObstacleCollisionCell(cell) || collisionCells.Contains(cell)))
                {
                    return false;
                }
            }

            if (obstacle.MinimumSpacing <= 0f && placements.Count == 0)
                return true;

            foreach (OpenFieldObstaclePlacement existing in placements)
            {
                float minimumSpacing = Mathf.Max(obstacle.MinimumSpacing, existing.MinimumSpacing);
                if (minimumSpacing <= 0f)
                    continue;

                float minimumSquaredDistance = minimumSpacing * minimumSpacing;
                foreach (ObstacleMaskCell maskCell in mask)
                {
                    Vector2Int candidateCell = origin + maskCell.LocalCell;
                    foreach (Vector2Int existingCell in existing.OccupiedCells)
                    {
                        float deltaX = candidateCell.x - existingCell.x;
                        float deltaY = candidateCell.y - existingCell.y;
                        if (deltaX * deltaX + deltaY * deltaY < minimumSquaredDistance)
                            return false;
                    }
                }
            }

            return true;
        }

        private static Random CreateRandom(int layoutSeed, int salt)
        {
            unchecked
            {
                uint value = (uint)layoutSeed;
                value ^= (uint)salt + 0x9E3779B9u + (value << 6) + (value >> 2);
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                return new Random((int)(value & 0x7FFFFFFFu));
            }
        }

        private static int DivideRoundUp(int numerator, int denominator)
        {
            return numerator / denominator + (numerator % denominator == 0 ? 0 : 1);
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int otherIndex = random.Next(index + 1);
                (values[index], values[otherIndex]) = (values[otherIndex], values[index]);
            }
        }

        private sealed class StyleSeed
        {
            public StyleSeed(int styleIndex, Vector2Int cell)
            {
                StyleIndex = styleIndex;
                Frontier = new List<Vector2Int> { cell };
            }

            public int StyleIndex { get; }
            public List<Vector2Int> Frontier { get; }
        }

        private sealed class ObstacleDefinitionState
        {
            public ObstacleDefinitionState(int obstacleIndex, OpenFieldObstacleData obstacle)
            {
                ObstacleIndex = obstacleIndex;
                Obstacle = obstacle;
            }

            public int ObstacleIndex { get; }
            public OpenFieldObstacleData Obstacle { get; }
            public int PlacedCount { get; set; }
        }

        private readonly struct ObstacleMaskCell
        {
            public ObstacleMaskCell(Vector2Int localCell, bool isCollision)
            {
                LocalCell = localCell;
                IsCollision = isCollision;
            }

            public Vector2Int LocalCell { get; }
            public bool IsCollision { get; }
        }
    }
}
