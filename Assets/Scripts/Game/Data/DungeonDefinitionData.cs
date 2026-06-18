using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public sealed class DungeonThemeData : DataRow
    {
        public string Name;
        public string ThemeKey;
        public int FloorStart = 1;
        public int FloorEnd = 10;

        public string CorridorMaterialPath;
        public string RoomMaterialPath;
        public string AnteRoomMaterialPath;
        public string WallMaterialPath;
        public string StartMarkerMaterialPath;

        public int Mob1PoolId = -1;
        public int Mob2PoolId = -1;
        public int Mob3PoolId = -1;

        public int Treasure1PoolId = -1;
        public int Treasure2PoolId = -1;
        public int Treasure3PoolId = -1;

        public List<int> BossRoomIds = new();

        public void EnsureValid()
        {
            FloorStart = Mathf.Max(1, FloorStart);
            FloorEnd = Mathf.Max(FloorStart, FloorEnd);
            BossRoomIds ??= new List<int>();
        }
    }

    [Serializable]
    public sealed class DungeonMonsterPoolData : DataRow
    {
        public string Name;
        public List<DungeonMonsterPoolEntryData> Entries = new();

        public void EnsureValid()
        {
            Entries ??= new List<DungeonMonsterPoolEntryData>();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i] ??= new DungeonMonsterPoolEntryData();
                Entries[i].Weight = Mathf.Max(1, Entries[i].Weight);
                Entries[i].MinFloor = Mathf.Max(1, Entries[i].MinFloor);
                Entries[i].MaxFloor = Mathf.Max(Entries[i].MinFloor, Entries[i].MaxFloor);
            }
        }
    }

    [Serializable]
    public sealed class DungeonMonsterPoolEntryData
    {
        public string UnitName;
        public int Weight = 1;
        public int MinFloor = 1;
        public int MaxFloor = 9999;
        public bool BossOnly;
    }

    [Serializable]
    public sealed class DungeonBossRoomData : DataRow
    {
        public string Name;
        public string ThemeKey;
        public int FloorBandStart = 1;
        public int FloorBandEnd = 1;
        public int Width = 24;
        public int Height = 18;
        public Int2Data PlayerSpawn = new(3, 3);
        public Int2Data ExitSpawn = new(20, 14);
        public Int2Data RewardSpawn = new(12, 14);
        public Int2Data BossSpawn = new(12, 9);
        public List<Int2Data> SupportSpawnPoints = new();
        public List<int> BossPoolIds = new();
        public int RewardTreasurePoolId = -1;

        public void EnsureValid()
        {
            FloorBandStart = Mathf.Max(1, FloorBandStart);
            FloorBandEnd = Mathf.Max(FloorBandStart, FloorBandEnd);
            Width = Mathf.Max(8, Width);
            Height = Mathf.Max(8, Height);
            SupportSpawnPoints ??= new List<Int2Data>();
            BossPoolIds ??= new List<int>();
        }
    }

    [Serializable]
    public sealed class DungeonTreasurePoolData : DataRow
    {
        public string Name;
        public List<DungeonTreasurePoolEntryData> Entries = new();

        public void EnsureValid()
        {
            Entries ??= new List<DungeonTreasurePoolEntryData>();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i] ??= new DungeonTreasurePoolEntryData();
                Entries[i].Weight = Mathf.Max(1, Entries[i].Weight);
                Entries[i].MinFloor = Mathf.Max(1, Entries[i].MinFloor);
                Entries[i].MaxFloor = Mathf.Max(Entries[i].MinFloor, Entries[i].MaxFloor);
                Entries[i].Rewards ??= new List<DungeonTreasureRewardEntryData>();
                for (int rewardIndex = 0; rewardIndex < Entries[i].Rewards.Count; rewardIndex++)
                {
                    Entries[i].Rewards[rewardIndex] ??= new DungeonTreasureRewardEntryData();
                    Entries[i].Rewards[rewardIndex].Chance = Mathf.Clamp01(Entries[i].Rewards[rewardIndex].Chance);
                    Entries[i].Rewards[rewardIndex].MinQuantity = Mathf.Max(0, Entries[i].Rewards[rewardIndex].MinQuantity);
                    Entries[i].Rewards[rewardIndex].MaxQuantity = Mathf.Max(
                        Entries[i].Rewards[rewardIndex].MinQuantity,
                        Entries[i].Rewards[rewardIndex].MaxQuantity);
                }
            }
        }
    }

    [Serializable]
    public sealed class DungeonTreasurePoolEntryData
    {
        public int Weight = 1;
        public int MinFloor = 1;
        public int MaxFloor = 9999;
        public List<DungeonTreasureRewardEntryData> Rewards = new();
    }

    [Serializable]
    public sealed class DungeonTreasureRewardEntryData
    {
        public DropRewardType RewardType;
        public int ItemId = -1;
        public float Chance = 1f;
        public int MinQuantity = 1;
        public int MaxQuantity = 1;
    }

    [Serializable]
    public struct Int2Data
    {
        public int X;
        public int Y;

        public Int2Data(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
