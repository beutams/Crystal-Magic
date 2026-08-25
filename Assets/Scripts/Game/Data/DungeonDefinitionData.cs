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
        public DungeonTileGridData VoidTileGrid = new();
        public DungeonTileGridData GroundTileGrid = new();
        public DungeonTileGridData ObstacleTileGrid = new();

        public void EnsureValid()
        {
            VoidTileGrid ??= new DungeonTileGridData();
            GroundTileGrid ??= new DungeonTileGridData();
            ObstacleTileGrid ??= new DungeonTileGridData();
            VoidTileGrid.EnsureSize(3, 3);
            GroundTileGrid.EnsureSize(3, 3);
            ObstacleTileGrid.EnsureSize(3, 3);
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