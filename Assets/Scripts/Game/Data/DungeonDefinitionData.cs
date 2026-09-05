using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [ReadOnlyData]
    [Serializable]
    public sealed class DungeonThemeData : DataRow
    {
        public string Name;
        public string ThemeKey;
        public int FloorStart = 1;
        public int FloorEnd = 10;
        public OpenFieldDungeonThemeData OpenField = new();

        public void EnsureValid()
        {
            Name ??= string.Empty;
            ThemeKey ??= string.Empty;
            FloorStart = Mathf.Max(1, FloorStart);
            FloorEnd = Mathf.Max(FloorStart, FloorEnd);
            OpenField ??= new OpenFieldDungeonThemeData();
            OpenField.EnsureValid();
        }
    }

    [Serializable]
    public sealed class OpenFieldDungeonThemeData
    {
        public OpenFieldDungeonTerrainConfig Terrain = new();
        public OpenFieldDungeonAnchorConfig Anchors = new();
        public OpenFieldDungeonContentConfig Content = new();
        public OpenFieldDungeonVisualData Visual = new();
        public List<OpenFieldDungeonLandmarkEntryData> Landmarks = new();
        public List<OpenFieldDungeonEncounterPoolData> EncounterPools = new();
        public List<int> TreasureItemIds = new();

        public void EnsureValid()
        {
            Terrain ??= new OpenFieldDungeonTerrainConfig();
            Terrain.EnsureValid();
            Anchors ??= new OpenFieldDungeonAnchorConfig();
            Anchors.EnsureValid();
            Content ??= new OpenFieldDungeonContentConfig();
            Content.EnsureValid();
            Visual ??= new OpenFieldDungeonVisualData();
            Visual.EnsureValid();
            Landmarks ??= new List<OpenFieldDungeonLandmarkEntryData>();
            EncounterPools ??= new List<OpenFieldDungeonEncounterPoolData>();
            TreasureItemIds ??= new List<int>();

            foreach (OpenFieldDungeonLandmarkEntryData entry in Landmarks)
                entry?.EnsureValid();
            foreach (OpenFieldDungeonEncounterPoolData pool in EncounterPools)
                pool?.EnsureValid();
        }
    }
    [Serializable]
    public sealed class OpenFieldDungeonVisualData
    {
        public OpenFieldVoidVisualData VoidVisual = new();
        public OpenFieldObstacleVisualData ObstacleVisual = new();
        public int GroundCellsPerStyleSeed = 480;
        public List<OpenFieldGroundStyleData> GroundStyles = new();

        public void EnsureValid()
        {
            VoidVisual ??= new OpenFieldVoidVisualData();
            VoidVisual.EnsureValid();
            ObstacleVisual ??= new OpenFieldObstacleVisualData();
            ObstacleVisual.EnsureValid();
            GroundCellsPerStyleSeed = Mathf.Max(1, GroundCellsPerStyleSeed);
            GroundStyles ??= new List<OpenFieldGroundStyleData>();
            for (int i = 0; i < GroundStyles.Count; i++)
            {
                GroundStyles[i] ??= new OpenFieldGroundStyleData();
                GroundStyles[i].EnsureValid();
            }
        }
    }

    [Serializable]
    public sealed class OpenFieldRuleTileReferenceData
    {
        public string AssetPath;

        public void EnsureValid()
        {
            AssetPath ??= string.Empty;
        }
    }

    [Serializable]
    public struct OpenFieldSpriteUvData
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public OpenFieldSpriteUvData(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Vector4 ToVector4()
        {
            return new Vector4(X, Y, Width, Height);
        }
    }

    [Serializable]
    public struct OpenFieldVector2Data
    {
        public float X;
        public float Y;

        public OpenFieldVector2Data(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2 ToVector2()
        {
            return new Vector2(X, Y);
        }
    }

    [Serializable]
    public sealed class OpenFieldSpriteReferenceData
    {
        public string AssetPath;
        public string SpriteName;
        public OpenFieldSpriteUvData SpriteUv;
        public bool HasSpriteUv;

        public void EnsureValid()
        {
            AssetPath ??= string.Empty;
            SpriteName ??= string.Empty;
        }
    }

    [Serializable]
    public sealed class OpenFieldObstacleSpriteCellData
    {
        public int X;
        public int Y;
        public bool UseObstacleCenter;
        public OpenFieldSpriteReferenceData Sprite = new();

        public void EnsureValid()
        {
            Sprite ??= new OpenFieldSpriteReferenceData();
            Sprite.EnsureValid();
        }
    }

    [Serializable]
    public sealed class OpenFieldObstacleSpriteLayerData
    {
        public string Name;
        public List<OpenFieldObstacleSpriteCellData> Cells = new();

        public void EnsureValid()
        {
            Name ??= string.Empty;
            Cells ??= new List<OpenFieldObstacleSpriteCellData>();
            HashSet<Vector2Int> occupiedCells = new();
            for (int index = Cells.Count - 1; index >= 0; index--)
            {
                Cells[index] ??= new OpenFieldObstacleSpriteCellData();
                Cells[index].EnsureValid();
                Vector2Int cell = new(Cells[index].X, Cells[index].Y);
                if (!occupiedCells.Add(cell))
                    Cells.RemoveAt(index);
            }
        }
    }

    [Serializable]
    public sealed class OpenFieldVoidVisualData
    {
        public OpenFieldRuleTileReferenceData AbyssRuleTile = new();
        public OpenFieldRuleTileReferenceData WallRuleTile = new();
        public OpenFieldRuleTileReferenceData TransitionRuleTile = new();

        public void EnsureValid()
        {
            AbyssRuleTile ??= new OpenFieldRuleTileReferenceData();
            AbyssRuleTile.EnsureValid();
            WallRuleTile ??= new OpenFieldRuleTileReferenceData();
            WallRuleTile.EnsureValid();
            TransitionRuleTile ??= new OpenFieldRuleTileReferenceData();
            TransitionRuleTile.EnsureValid();
        }
    }

    [Serializable]
    public sealed class OpenFieldObstacleVisualData
    {
        public OpenFieldRuleTileReferenceData TopRuleTile = new();
        public OpenFieldRuleTileReferenceData WallRuleTile = new();
        public OpenFieldRuleTileReferenceData TransitionRuleTile = new();

        public void EnsureValid()
        {
            TopRuleTile ??= new OpenFieldRuleTileReferenceData();
            TopRuleTile.EnsureValid();
            WallRuleTile ??= new OpenFieldRuleTileReferenceData();
            WallRuleTile.EnsureValid();
            TransitionRuleTile ??= new OpenFieldRuleTileReferenceData();
            TransitionRuleTile.EnsureValid();
        }
    }

    [Serializable]
    public sealed class OpenFieldGroundStyleData
    {
        public string Name;
        public OpenFieldRuleTileReferenceData BaseRuleTile = new();
        public List<OpenFieldDecorationData> Decorations = new();
        public List<OpenFieldObstacleData> Obstacles = new();

        public void EnsureValid()
        {
            Name ??= string.Empty;
            BaseRuleTile ??= new OpenFieldRuleTileReferenceData();
            BaseRuleTile.EnsureValid();
            Decorations ??= new List<OpenFieldDecorationData>();
            for (int i = 0; i < Decorations.Count; i++)
            {
                Decorations[i] ??= new OpenFieldDecorationData();
                Decorations[i].EnsureValid();
            }

            Obstacles ??= new List<OpenFieldObstacleData>();
            for (int i = 0; i < Obstacles.Count; i++)
            {
                Obstacles[i] ??= new OpenFieldObstacleData();
                Obstacles[i].EnsureValid();
            }
        }
    }

    [Serializable]
    public sealed class OpenFieldDecorationData
    {
        public string Name;
        public OpenFieldRuleTileReferenceData RuleTile = new();
        public float Radius = 8f;
        public int MaximumSpread = 8;

        public void EnsureValid()
        {
            Name ??= string.Empty;
            RuleTile ??= new OpenFieldRuleTileReferenceData();
            RuleTile.EnsureValid();
            Radius = Mathf.Max(0.01f, Radius);
            MaximumSpread = Mathf.Max(0, MaximumSpread);
        }
    }

    [Serializable]
    public sealed class OpenFieldObstacleData
    {
        public string Name;
        // Legacy data is migrated into SpriteLayers the first time this row is validated.
        public OpenFieldSpriteReferenceData Sprite = new();
        public List<OpenFieldObstacleSpriteLayerData> SpriteLayers = new();
        public int FootprintWidth = 1;
        public int FootprintHeight = 1;
        public List<bool> CollisionMask = new();
        public int Weight = 1;
        public float MinimumSpacing;
        public int MaximumCount = 1;
        public bool AllowRotation;
        public bool AllowFlipX;
        public OpenFieldVector2Data VisualSortAnchor;

        public void EnsureValid()
        {
            Name ??= string.Empty;
            Sprite ??= new OpenFieldSpriteReferenceData();
            Sprite.EnsureValid();
            SpriteLayers ??= new List<OpenFieldObstacleSpriteLayerData>();
            if (SpriteLayers.Count == 0 && !string.IsNullOrWhiteSpace(Sprite.AssetPath))
            {
                SpriteLayers.Add(new OpenFieldObstacleSpriteLayerData
                {
                    Name = "Layer 1",
                    Cells = new List<OpenFieldObstacleSpriteCellData>
                    {
                        new() { X = 0, Y = 0, UseObstacleCenter = true, Sprite = Sprite },
                    },
                });
                Sprite = new OpenFieldSpriteReferenceData();
            }

            for (int index = 0; index < SpriteLayers.Count; index++)
            {
                SpriteLayers[index] ??= new OpenFieldObstacleSpriteLayerData();
                SpriteLayers[index].EnsureValid();
            }

            FootprintWidth = Mathf.Max(1, FootprintWidth);
            FootprintHeight = Mathf.Max(1, FootprintHeight);
            Weight = Mathf.Max(1, Weight);
            MinimumSpacing = Mathf.Max(0f, MinimumSpacing);
            MaximumCount = Mathf.Max(0, MaximumCount);

            int cellCount = FootprintWidth * FootprintHeight;
            CollisionMask ??= new List<bool>();
            while (CollisionMask.Count < cellCount)
                CollisionMask.Add(false);
            if (CollisionMask.Count > cellCount)
                CollisionMask.RemoveRange(cellCount, CollisionMask.Count - cellCount);
        }
    }

    [Serializable]
    public sealed class OpenFieldDungeonLandmarkEntryData
    {
        public string PrefabName;
        public int Weight = 1;
        public int FootprintWidth = 1;
        public int FootprintHeight = 1;
        public bool ApplyCollider = true;
        public int MinInstances;
        public int MaxInstances = 1;

        public void EnsureValid()
        {
            PrefabName ??= string.Empty;
            Weight = Mathf.Max(1, Weight);
            FootprintWidth = Mathf.Max(1, FootprintWidth);
            FootprintHeight = Mathf.Max(1, FootprintHeight);
            MinInstances = Mathf.Max(0, MinInstances);
            MaxInstances = Mathf.Max(MinInstances, MaxInstances);
        }
    }

    public enum OpenFieldInterestSizeData : byte
    {
        Small,
        Medium,
        Large,
    }

    [Serializable]
    public sealed class OpenFieldDungeonEncounterPoolData
    {
        public string Name;
        public OpenFieldInterestSizeData InterestSize;
        public List<OpenFieldDungeonSquadData> Squads = new();

        public void EnsureValid()
        {
            Name ??= string.Empty;
            Squads ??= new List<OpenFieldDungeonSquadData>();
            foreach (OpenFieldDungeonSquadData squad in Squads)
                squad?.EnsureValid();
        }
    }

    [Serializable]
    public sealed class OpenFieldDungeonSquadData
    {
        public string Name;
        public int Weight = 1;
        public int MonsterLevel = 1;
        public bool IsBossSquad;
        public int Width = 3;
        public int Height = 3;
        public List<OpenFieldDungeonSquadMemberData> Members = new();

        public void EnsureValid()
        {
            Name ??= string.Empty;
            Weight = Mathf.Max(1, Weight);
            MonsterLevel = Mathf.Clamp(MonsterLevel, 1, 3);
            Width = Mathf.Max(1, Width);
            Height = Mathf.Max(1, Height);
            Members ??= new List<OpenFieldDungeonSquadMemberData>();
            foreach (OpenFieldDungeonSquadMemberData member in Members)
                member?.EnsureValid();
        }
    }

    [Serializable]
    public sealed class OpenFieldDungeonSquadMemberData
    {
        public string UnitName;
        public int Count = 1;

        public void EnsureValid()
        {
            UnitName ??= string.Empty;
            Count = Mathf.Max(1, Count);
        }
    }
}
