using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public sealed class DungeonTileGridData
    {
        public int Columns = 3;
        public int Rows = 3;
        public List<DungeonTileGridCellData> Cells = new();

        public void EnsureValid()
        {
            EnsureSize(Columns, Rows);
        }

        public void EnsureSize(int columns, int rows)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            Cells ??= new List<DungeonTileGridCellData>();

            if (Columns != columns || Rows != rows)
            {
                int oldColumns = Mathf.Max(1, Columns);
                int oldRows = Mathf.Max(1, Rows);
                List<DungeonTileGridCellData> oldCells = Cells;
                Cells = new List<DungeonTileGridCellData>(columns * rows);
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        DungeonTileGridCellData cell = x < oldColumns && y < oldRows
                            && y * oldColumns + x < oldCells.Count
                            ? oldCells[y * oldColumns + x]
                            : null;
                        Cells.Add(cell ?? new DungeonTileGridCellData());
                    }
                }
            }

            Columns = columns;
            Rows = rows;
            int cellCount = Columns * Rows;
            while (Cells.Count < cellCount)
                Cells.Add(new DungeonTileGridCellData());
            if (Cells.Count > cellCount)
                Cells.RemoveRange(cellCount, Cells.Count - cellCount);

            for (int i = 0; i < Cells.Count; i++)
            {
                Cells[i] ??= new DungeonTileGridCellData();
                Cells[i].SpritePath ??= string.Empty;
                Cells[i].SpriteName ??= string.Empty;
            }
        }

        public DungeonTileGridCellData GetCell(int column, int row)
        {
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
                return null;

            return Cells[row * Columns + column];
        }

        public bool HasAssignedSprite()
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(Cells[i]?.SpritePath)
                    && !string.IsNullOrWhiteSpace(Cells[i]?.SpriteName))
                    return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class DungeonTileGridCellData
    {
        public string SpritePath;
        public string SpriteName;
        public float SpriteUvX;
        public float SpriteUvY;
        public float SpriteUvWidth;
        public float SpriteUvHeight;
        public bool HasCollision;

        public bool HasSpriteUv => SpriteUvWidth > 0f && SpriteUvHeight > 0f;

        public void SetSpriteUv(Rect textureRect, int textureWidth, int textureHeight)
        {
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                SpriteUvX = 0f;
                SpriteUvY = 0f;
                SpriteUvWidth = 0f;
                SpriteUvHeight = 0f;
                return;
            }

            SpriteUvX = textureRect.x / textureWidth;
            SpriteUvY = textureRect.y / textureHeight;
            SpriteUvWidth = textureRect.width / textureWidth;
            SpriteUvHeight = textureRect.height / textureHeight;
        }
    }
}