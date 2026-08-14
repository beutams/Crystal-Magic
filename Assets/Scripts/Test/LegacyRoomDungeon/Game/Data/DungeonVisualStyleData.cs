#if LEGACY_ROOM_DUNGEON_REFERENCE
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    public enum DungeonFloorTileRole
    {
        Center = 0,
        Top = 1,
        Bottom = 2,
        Left = 3,
        Right = 4,
        TopLeft = 5,
        TopRight = 6,
        BottomLeft = 7,
        BottomRight = 8,
    }

    [Flags]
    public enum DungeonEdgeAnchor
    {
        None = 0,
        TopLeft = 1 << 0,
        Top = 1 << 1,
        TopRight = 1 << 2,
        Right = 1 << 3,
        BottomRight = 1 << 4,
        Bottom = 1 << 5,
        BottomLeft = 1 << 6,
        Left = 1 << 7,
    }

    [Serializable]
    public sealed class DungeonVisualStyleData
    {
        public int Id = 1;
        public string Name;
        public string StyleKey;
        public DungeonTileSetData TileSet = new();
        public DungeonWallTileSetData WallTileSet = new();
        public DungeonDoorTileSetData DoorTileSet = new();
        public List<DungeonCorridorVisualData> Corridors = new();
        public List<DungeonAreaVisualData> RoomVisuals = new();
        public List<DungeonAreaVisualData> AnteRoomVisuals = new();
        public List<DungeonInteriorProfileData> RoomProfiles = new();
        public List<DungeonInteriorProfileData> AnteRoomProfiles = new();
        public List<DungeonVisualStyleTransitionData> ChildStyleTransitions = new();

        public void EnsureValid()
        {
            StyleKey ??= string.Empty;
            TileSet ??= new DungeonTileSetData();
            TileSet.EnsureValid();
            WallTileSet ??= new DungeonWallTileSetData();
            WallTileSet.EnsureValid();
            DoorTileSet ??= new DungeonDoorTileSetData();
            DoorTileSet.EnsureValid();
            Corridors ??= new List<DungeonCorridorVisualData>();
            RoomVisuals ??= new List<DungeonAreaVisualData>();
            AnteRoomVisuals ??= new List<DungeonAreaVisualData>();
            RoomProfiles ??= new List<DungeonInteriorProfileData>();
            AnteRoomProfiles ??= new List<DungeonInteriorProfileData>();
            ChildStyleTransitions ??= new List<DungeonVisualStyleTransitionData>();

            EnsureEntries(Corridors, static entry => entry.EnsureValid());
            EnsureEntries(RoomVisuals, static entry => entry.EnsureValid());
            EnsureEntries(AnteRoomVisuals, static entry => entry.EnsureValid());
            EnsureEntries(RoomProfiles, static entry => entry.EnsureValid());
            EnsureEntries(AnteRoomProfiles, static entry => entry.EnsureValid());
            for (int i = 0; i < ChildStyleTransitions.Count; i++)
            {
                ChildStyleTransitions[i] ??= new DungeonVisualStyleTransitionData();
                ChildStyleTransitions[i].Weight = Mathf.Max(1, ChildStyleTransitions[i].Weight);
            }
        }

        private static void EnsureEntries<T>(List<T> entries, Action<T> ensure) where T : class, new()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i] ??= new T();
                ensure(entries[i]);
            }
        }
    }

    [Serializable]
    public sealed class DungeonCorridorVisualData
    {
        public int Width = 1;
        public string MaterialPath;
        public int Weight = 1;
        public DungeonTileGridData TileGrid = new();

        public void EnsureValid()
        {
            Width = Width <= 1 ? 1 : Width <= 3 ? 3 : 5;
            MaterialPath ??= string.Empty;
            Weight = Mathf.Max(1, Weight);
            TileGrid ??= new DungeonTileGridData();
            TileGrid.EnsureSize(Width, Width);
        }
    }

    [Serializable]
    public sealed class DungeonAreaVisualData
    {
        public int MinWidth = 1;
        public int MaxWidth = 999;
        public string MaterialPath;
        public int Weight = 1;
        public DungeonTileGridData TileGrid = new();

        public void EnsureValid()
        {
            MinWidth = Mathf.Max(1, MinWidth);
            MaxWidth = Mathf.Max(MinWidth, MaxWidth);
            MaterialPath ??= string.Empty;
            Weight = Mathf.Max(1, Weight);
            TileGrid ??= new DungeonTileGridData();
            TileGrid.EnsureValid();
        }
    }

    [Serializable]
    public sealed class DungeonTileGridData
    {
        public int Columns = 3;
        public int Rows = 3;
        public List<DungeonTileGridCellData> Cells = new();

        public void EnsureValid()
        {
            EnsureSize(Mathf.Max(1, Columns), Mathf.Max(1, Rows));
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

    [Serializable]
    public sealed class DungeonWallTileSetData
    {
        public DungeonTileGridData TileGrid = new();
        public bool CollisionDefaultsApplied;

        public void EnsureValid()
        {
            TileGrid ??= new DungeonTileGridData();
            TileGrid.EnsureSize(3, 3);
            if (!CollisionDefaultsApplied)
            {
                for (int i = 0; i < TileGrid.Cells.Count; i++)
                {
                    if (i != 4)
                        TileGrid.Cells[i].HasCollision = true;
                }

                CollisionDefaultsApplied = true;
            }

            DungeonTileGridCellData center = TileGrid.GetCell(1, 1);
            if (center == null)
                return;

            center.SpritePath = string.Empty;
            center.SpriteName = string.Empty;
            center.HasCollision = false;
        }
    }

    [Serializable]
    public sealed class DungeonDoorTileSetData
    {
        public DungeonTileGridData Horizontal = new();
        public DungeonTileGridData Vertical = new();

        public void EnsureValid()
        {
            Horizontal ??= new DungeonTileGridData();
            Vertical ??= new DungeonTileGridData();
            Horizontal.EnsureSize(1, 1);
            Vertical.EnsureSize(1, 1);
        }
    }

    [Serializable]
    public sealed class DungeonVisualStyleTransitionData
    {
        public int StyleId = 1;
        public int Weight = 1;
    }

    [Serializable]
    public sealed class DungeonTileSetData
    {
        public string Name;
        public string RoomMaterialPath;
        public string AnteRoomMaterialPath;
        public string WallMaterialPath;
        public string DoorPrefabName;
        public string PreviewSpritePath;
        public List<DungeonFloorTileSpriteData> FloorTiles = new();
        public List<DungeonWallTileSpriteData> WallTiles = new();

        public void EnsureValid()
        {
            RoomMaterialPath ??= string.Empty;
            AnteRoomMaterialPath ??= string.Empty;
            WallMaterialPath ??= string.Empty;
            DoorPrefabName ??= string.Empty;
            PreviewSpritePath ??= string.Empty;
            FloorTiles ??= new List<DungeonFloorTileSpriteData>();
            WallTiles ??= new List<DungeonWallTileSpriteData>();

            for (int i = 0; i < FloorTiles.Count; i++)
            {
                FloorTiles[i] ??= new DungeonFloorTileSpriteData();
                FloorTiles[i].SpritePath ??= string.Empty;
            }

            for (int i = 0; i < WallTiles.Count; i++)
            {
                WallTiles[i] ??= new DungeonWallTileSpriteData();
                WallTiles[i].NeighborMask = Mathf.Clamp(WallTiles[i].NeighborMask, 0, 15);
                WallTiles[i].SpritePath ??= string.Empty;
            }
        }
    }

    [Serializable]
    public sealed class DungeonFloorTileSpriteData
    {
        public DungeonFloorTileRole Role;
        public string SpritePath;
    }

    [Serializable]
    public sealed class DungeonWallTileSpriteData
    {
        public int NeighborMask;
        public string SpritePath;
    }

    [Serializable]
    public sealed class DungeonInteriorProfileData
    {
        public string Name;
        public int MinWidth = 5;
        public int MinHeight = 5;
        public int Weight = 1;
        public List<DungeonMainDecorationData> MainDecorations = new();
        public List<DungeonSecondaryDecorationData> SecondaryDecorations = new();
        public List<DungeonEdgeDecorationData> EdgeDecorations = new();

        public void EnsureValid()
        {
            MinWidth = Mathf.Max(1, MinWidth);
            MinHeight = Mathf.Max(1, MinHeight);
            Weight = Mathf.Max(1, Weight);
            MainDecorations ??= new List<DungeonMainDecorationData>();
            SecondaryDecorations ??= new List<DungeonSecondaryDecorationData>();
            EdgeDecorations ??= new List<DungeonEdgeDecorationData>();
            EnsureDecorationEntries(MainDecorations, static entry => entry.EnsureValid());
            EnsureDecorationEntries(SecondaryDecorations, static entry => entry.EnsureValid());
            EnsureDecorationEntries(EdgeDecorations, static entry => entry.EnsureValid());
        }

        private static void EnsureDecorationEntries<T>(List<T> entries, Action<T> ensure) where T : class, new()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i] ??= new T();
                ensure(entries[i]);
            }
        }
    }

    [Serializable]
    public sealed class DungeonMainDecorationData
    {
        public string PrefabName;
        public int Width = 1;
        public int Height = 1;
        public int Weight = 1;

        public void EnsureValid()
        {
            PrefabName ??= string.Empty;
            Width = Mathf.Max(1, Width);
            Height = Mathf.Max(1, Height);
            Weight = Mathf.Max(1, Weight);
        }
    }

    [Serializable]
    public sealed class DungeonSecondaryDecorationData
    {
        public string PrefabName;
        public int Width = 1;
        public int Height = 1;
        public int MinPairs = 1;
        public int MaxPairs = 1;
        public int MinDistance = 2;
        public int MaxDistance = 4;
        public int Weight = 1;

        public void EnsureValid()
        {
            PrefabName ??= string.Empty;
            Width = Mathf.Max(1, Width);
            Height = Mathf.Max(1, Height);
            MinPairs = Mathf.Max(0, MinPairs);
            MaxPairs = Mathf.Max(MinPairs, MaxPairs);
            MinDistance = Mathf.Max(1, MinDistance);
            MaxDistance = Mathf.Max(MinDistance, MaxDistance);
            Weight = Mathf.Max(1, Weight);
        }
    }

    [Serializable]
    public sealed class DungeonEdgeDecorationData
    {
        public string PrefabName;
        public int Width = 1;
        public int Height = 1;
        public int AnchorX;
        public int AnchorY;
        public int AnchorWidth = 1;
        public int AnchorHeight = 1;
        public DungeonEdgeAnchor AllowedAnchors = DungeonEdgeAnchor.TopLeft
            | DungeonEdgeAnchor.TopRight
            | DungeonEdgeAnchor.BottomLeft
            | DungeonEdgeAnchor.BottomRight;
        public int MaxInstances = 1;
        public int Weight = 1;

        public void EnsureValid()
        {
            PrefabName ??= string.Empty;
            Width = Mathf.Max(1, Width);
            Height = Mathf.Max(1, Height);
            AnchorX = Mathf.Clamp(AnchorX, 0, Width - 1);
            AnchorY = Mathf.Clamp(AnchorY, 0, Height - 1);
            AnchorWidth = Mathf.Clamp(AnchorWidth, 1, Width - AnchorX);
            AnchorHeight = Mathf.Clamp(AnchorHeight, 1, Height - AnchorY);
            MaxInstances = Mathf.Max(1, MaxInstances);
            Weight = Mathf.Max(1, Weight);
        }
    }
}

#endif
