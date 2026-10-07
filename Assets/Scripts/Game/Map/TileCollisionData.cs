using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Map
{
    // One cell mask is the source of truth for both physics and navigation.
    public sealed class TileCollisionData : ScriptableObject
    {
        public Vector2Int MinCell;
        public int Width;
        public int Height;
        public float CellSize = 1;
        public Vector3 GridOrigin;
        public float Depth = 1.6f;
        public List<Vector3Int> BlockedCells = new();
        public string Summary;

        public Vector3 CellCorner(Vector3Int cell) => GridOrigin + new Vector3(cell.x, cell.y, 0) * CellSize;
        public Vector3 MinCorner => CellCorner(new Vector3Int(MinCell.x, MinCell.y, 0));
        public Rect LocalBounds => new Rect(MinCorner.x, MinCorner.y, Width * CellSize, Height * CellSize);

        public void Validate()
        {
            if (Width <= 0 || Height <= 0 || (long)Width * Height > 250000 ||
                !float.IsFinite(CellSize) || CellSize <= 0 || !float.IsFinite(Depth) || Depth <= 0 ||
                !float.IsFinite(GridOrigin.x) || !float.IsFinite(GridOrigin.y) || !float.IsFinite(GridOrigin.z))
                throw new InvalidOperationException("无效的碰撞网格尺寸。");
            foreach (var cell in BlockedCells)
                if (cell.z != 0 || cell.x < MinCell.x || cell.y < MinCell.y ||
                    cell.x >= MinCell.x + Width || cell.y >= MinCell.y + Height)
                    throw new InvalidOperationException("碰撞格超出地图范围。");
        }
    }
}
