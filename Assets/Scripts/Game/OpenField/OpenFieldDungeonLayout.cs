using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.OpenField
{
    public enum OpenFieldTerrainCell : byte
    {
        Void = 0,
        Ground = 1,
        Obstacle = 2,
    }

    public sealed class OpenFieldDungeonLayout
    {
        private readonly float[] _terrainValues;
        private readonly OpenFieldTerrainCell[] _terrainCells;
        private readonly int[] _heightSteps;
        private readonly bool[] _reachableCells;
        private readonly List<OpenFieldInterestPoint> _interestPoints = new();
        private readonly List<OpenFieldContentPlacement> _contentPlacements = new();

        internal OpenFieldDungeonLayout(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Seed = seed;
            _terrainValues = new float[width * height];
            _terrainCells = new OpenFieldTerrainCell[width * height];
            _heightSteps = new int[width * height];
            _reachableCells = new bool[width * height];
        }

        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        public int CellCount => _terrainCells.Length;
        public bool HasEntrance { get; private set; }
        public OpenFieldGridPosition Entrance { get; private set; }
        public int EntranceRadius { get; private set; }
        public IReadOnlyList<OpenFieldInterestPoint> InterestPoints => _interestPoints;
        public OpenFieldInterestPoint ExitInterestPoint { get; private set; }
        public IReadOnlyList<OpenFieldContentPlacement> ContentPlacements => _contentPlacements;

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public int GetIndex(int x, int y)
        {
            if (!IsInside(x, y))
                throw new ArgumentOutOfRangeException($"Cell ({x}, {y}) is outside {Width} x {Height}.");

            return y * Width + x;
        }

        public float GetTerrainValue(int x, int y)
        {
            return _terrainValues[GetIndex(x, y)];
        }

        public OpenFieldTerrainCell GetTerrainCell(int x, int y)
        {
            return _terrainCells[GetIndex(x, y)];
        }

        public int GetHeightSteps(int x, int y)
        {
            return _heightSteps[GetIndex(x, y)];
        }

        public bool IsWalkable(int x, int y)
        {
            return IsInside(x, y) && GetTerrainCell(x, y) == OpenFieldTerrainCell.Ground;
        }

        public bool BlocksLineOfSight(int x, int y)
        {
            return IsInside(x, y) && GetTerrainCell(x, y) == OpenFieldTerrainCell.Obstacle;
        }

        public bool IsReachable(int x, int y)
        {
            return IsInside(x, y) && _reachableCells[GetIndex(x, y)];
        }

        internal bool SetEntrance(OpenFieldGridPosition center, int radius)
        {
            if (HasEntrance)
                return false;

            Entrance = center;
            EntranceRadius = radius;
            HasEntrance = true;
            return true;
        }

        internal void AddInterestPoint(OpenFieldInterestSize size, OpenFieldGridPosition center, int radius)
        {
            _interestPoints.Add(new OpenFieldInterestPoint(_interestPoints.Count + 1, size, center, radius));
        }

        internal List<OpenFieldInterestPoint> GetPoints(OpenFieldInterestSize size)
        {
            List<OpenFieldInterestPoint> results = new();
            foreach (OpenFieldInterestPoint point in _interestPoints)
            {
                if (point.Size == size)
                    results.Add(point);
            }

            return results;
        }

        internal void SetExitInterestPoint(OpenFieldInterestPoint point)
        {
            ExitInterestPoint = point;
            point.IsExitInterestPoint = true;
        }

        internal void ClearAnchors()
        {
            HasEntrance = false;
            Entrance = default;
            EntranceRadius = 0;
            ExitInterestPoint = null;
            _interestPoints.Clear();
            _contentPlacements.Clear();
            ClearReachability();
        }

        internal void ClearReachability()
        {
            Array.Clear(_reachableCells, 0, _reachableCells.Length);
        }

        internal void MarkReachable(int x, int y)
        {
            _reachableCells[GetIndex(x, y)] = true;
        }
        internal void AddContent(OpenFieldContentType type, OpenFieldGridPosition cell, int encounterId, int squadId)
        {
            _contentPlacements.Add(new OpenFieldContentPlacement(type, cell, encounterId, squadId));
        }

        internal void ClearContent()
        {
            _contentPlacements.Clear();
        }
        internal void SetTerrain(int x, int y, float terrainValue, OpenFieldTerrainCell terrainCell, int heightSteps)
        {
            int index = GetIndex(x, y);
            _terrainValues[index] = terrainValue;
            _terrainCells[index] = terrainCell;
            _heightSteps[index] = terrainCell switch
            {
                OpenFieldTerrainCell.Void => -1,
                OpenFieldTerrainCell.Ground => 0,
                OpenFieldTerrainCell.Obstacle => Math.Max(1, heightSteps),
                _ => throw new ArgumentOutOfRangeException(nameof(terrainCell), terrainCell, null),
            };
        }
    }
}
