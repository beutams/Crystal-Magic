using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.MapDemo
{
    public enum DungeonMakerDirection
    {
        NO = 0,
        EA = 1,
        SO = 2,
        WE = 3,
        NE = 4,
        SE = 5,
        SW = 6,
        NW = 7,
        XX = 8,
    }

    [Serializable]
    public struct DungeonMakerTripleInt
    {
        public int Small;
        public int Medium;
        public int Large;

        public DungeonMakerTripleInt(int small, int medium, int large)
        {
            Small = small;
            Medium = medium;
            Large = large;
        }
    }

    [Serializable]
    public sealed class DungeonMakerVisualStyleRuleData
    {
        public int StyleId = -1;
        public List<DungeonMakerVisualStyleWeightData> ChildStyleWeights = new();
    }

    [Serializable]
    public sealed class DungeonMakerVisualStyleWeightData
    {
        public int StyleId = -1;
        public int Weight = 1;
    }

    [Serializable]
    public struct DungeonMakerTunnelerSeedData
    {
        public Vector2Int Location;
        public DungeonMakerDirection Direction;
        public DungeonMakerDirection IntendedDirection;
        public int Age;
        public int MaxAge;
        public int Generation;
        public int StepLength;
        public int TunnelWidth;
        public int StraightDoubleSpawnProb;
        public int TurnDoubleSpawnProb;
        public int ChangeDirectionProb;
        public int MakeRoomsRightProb;
        public int MakeRoomsLeftProb;
        public int JoinPreference;

        public DungeonMakerTunnelerSeedData(
            Vector2Int location,
            DungeonMakerDirection direction,
            DungeonMakerDirection intendedDirection,
            int age,
            int maxAge,
            int generation,
            int stepLength,
            int tunnelWidth,
            int straightDoubleSpawnProb,
            int turnDoubleSpawnProb,
            int changeDirectionProb,
            int makeRoomsRightProb,
            int makeRoomsLeftProb,
            int joinPreference)
        {
            Location = location;
            Direction = direction;
            IntendedDirection = intendedDirection;
            Age = age;
            MaxAge = maxAge;
            Generation = generation;
            StepLength = stepLength;
            TunnelWidth = tunnelWidth;
            StraightDoubleSpawnProb = straightDoubleSpawnProb;
            TurnDoubleSpawnProb = turnDoubleSpawnProb;
            ChangeDirectionProb = changeDirectionProb;
            MakeRoomsRightProb = makeRoomsRightProb;
            MakeRoomsLeftProb = makeRoomsLeftProb;
            JoinPreference = joinPreference;
        }
    }

    [Serializable]
    public sealed class DungeonMakerTunnelingConfig
    {
        [Header("地图基础")]
        public int DimX = 80;
        public int DimY = 200;
        public DungeonMakerSquareData Background = DungeonMakerSquareData.CLOSED;

        [Header("子代延迟")]
        public List<int> BabyDelayProbsTunneler = new() { 0, 20, 30, 50, 0, 0, 0, 0, 0, 0, 0 };
        public List<int> BabyDelayProbsRoomie = new() { 0, 0, 0, 50, 50, 0, 0, 0, 0, 0, 0 };
        public List<int> MaxAgesT = new()
        {
            5, 12, 12, 15, 15, 15, 15, 15, 15, 20, 30,
            10, 15, 10, 10, 20, 10, 10, 15, 10, 10,
            20, 20, 20, 20, 10, 20, 10, 20, 5,
        };

        [Header("房间大小概率")]
        public List<DungeonMakerTripleInt> RoomSizeProbS = new()
        {
            new DungeonMakerTripleInt(100, 0, 0),
            new DungeonMakerTripleInt(100, 0, 0),
            new DungeonMakerTripleInt(70, 30, 0),
            new DungeonMakerTripleInt(50, 50, 0),
            new DungeonMakerTripleInt(0, 50, 50),
            new DungeonMakerTripleInt(0, 0, 100),
        };

        public List<DungeonMakerTripleInt> RoomSizeProbB = new()
        {
            new DungeonMakerTripleInt(100, 0, 0),
            new DungeonMakerTripleInt(50, 50, 0),
            new DungeonMakerTripleInt(0, 30, 70),
            new DungeonMakerTripleInt(0, 0, 100),
        };

        [Header("隧道行为")]
        public List<int> JoinPref = new() { 0, 0, 10, 100, 100 };
        public List<int> SizeUpProb = new() { 0, 10, 10, 10, 20, 30, 40 };
        public List<int> SizeDownProb = new() { 0, 0, 20, 50, 60, 50, 60 };
        public List<int> AnteRoomProb = new() { 20, 20, 50, 0, 0, 100 };
        public int Mutator = 20;
        public int TunnelJoinDist = 18;
        public int Patience = 90;
        public int SizeUpGenDelay = 1;
        public bool ColumnsInTunnels = false;
        public double RoomAspectRatio = 0.6;
        public int GenSpeedUpOnAnteRoom = 2;

        public int MinCorridorWidth = 1;
        public int MaxCorridorWidth = 3;
        public int MinAnteRoomSide = 5;
        public int MaxAnteRoomSide = 7;
        public int MinRoomLength = 5;
        public int MaxRoomLength = 22;
        public int MinRoomWidth = 5;
        public int MaxRoomWidth = 22;

        [Header("Visual Styles")]
        public int RootVisualStyleId = -1;
        public List<DungeonMakerVisualStyleRuleData> VisualStyleRules = new();

        [Header("房间限制")]
        public int MinSmallRoomSize = 20;
        public int MinMediumRoomSize = 50;
        public int MinLargeRoomSize = 100;
        public int MaxRoomSize = 300;
        public int MaxSmallDungeonRooms = 100;
        public int MaxMediumDungeonRooms = 20;
        public int MaxLargeDungeonRooms = 2;

        [Header("额外链路")]
        public int TunnelCrawlerGeneration = -1;
        public DungeonMakerTunnelerSeedData LastChanceTunneler = new(
            new Vector2Int(0, 0),
            DungeonMakerDirection.XX,
            DungeonMakerDirection.XX,
            0,
            0,
            0,
            3,
            0,
            0,
            30,
            30,
            80,
            80,
            100);
        public int LastChanceGenerationalDelay = 4;

        [Header("初始施工队")]
        public DungeonMakerTunnelerSeedData[] Tunnelers =
        {
            new DungeonMakerTunnelerSeedData(
                new Vector2Int(40, 2),
                DungeonMakerDirection.EA,
                DungeonMakerDirection.EA,
                0,
                16,
                0,
                5,
                1,
                25,
                50,
                30,
                100,
                100,
                100),
        };

        public static DungeonMakerTunnelingConfig CreateDefault()
        {
            return new DungeonMakerTunnelingConfig();
        }
    }
}
