using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("Dungeon Generation Config")]
    public sealed class DungeonGenerationConfig
    {
        [EditorLabel("Map Width")]
        public int DimX = 80;

        [EditorLabel("Map Height")]
        public int DimY = 200;

        [EditorLabel("Min Small Room Size")]
        public int MinSmallRoomSize = 20;

        [EditorLabel("Min Medium Room Size")]
        public int MinMediumRoomSize = 50;

        [EditorLabel("Min Large Room Size")]
        public int MinLargeRoomSize = 100;

        [EditorLabel("Max Room Size")]
        public int MaxRoomSize = 300;

        [EditorLabel("Base Max Small Rooms")]
        public int MaxSmallDungeonRooms = 100;

        [EditorLabel("Base Max Medium Rooms")]
        public int MaxMediumDungeonRooms = 20;

        [EditorLabel("Base Max Large Rooms")]
        public int MaxLargeDungeonRooms = 2;

        [EditorLabel("Min Small Room Cap")]
        public int MinSmallDungeonRooms = 12;

        [EditorLabel("Min Medium Room Cap")]
        public int MinMediumDungeonRooms = 6;

        [EditorLabel("Min Large Room Cap")]
        public int MinLargeDungeonRooms = 1;

        [EditorLabel("Small Room Add Per Floor")]
        public int SmallRoomAddPerFloor = 1;

        [EditorLabel("Medium Room Add Floor Interval")]
        public int MediumRoomAddFloorInterval = 3;

        [EditorLabel("Large Room Add Floor Interval")]
        public int LargeRoomAddFloorInterval = 5;

        [EditorLabel("Tunnel Join Distance")]
        public int TunnelJoinDist = 18;

        [EditorLabel("Patience")]
        public int Patience = 90;

        [EditorLabel("Mutator")]
        public int Mutator = 20;

        [EditorLabel("Room Aspect Ratio")]
        public float RoomAspectRatio = 0.6f;

        [EditorLabel("Min Corridor Width")]
        public int MinCorridorWidth = 1;

        [EditorLabel("Max Corridor Width")]
        public int MaxCorridorWidth = 3;

        [EditorLabel("Min Ante Room Side")]
        public int MinAnteRoomSide = 5;

        [EditorLabel("Max Ante Room Side")]
        public int MaxAnteRoomSide = 7;

        [EditorLabel("Min Room Length")]
        public int MinRoomLength = 5;

        [EditorLabel("Max Room Length")]
        public int MaxRoomLength = 22;

        [EditorLabel("Min Room Width")]
        public int MinRoomWidth = 5;

        [EditorLabel("Max Room Width")]
        public int MaxRoomWidth = 22;

        [EditorLabel("Large Room Min")]
        public int LargeRoomMin = 5;

        [EditorLabel("Large Room Max")]
        public int LargeRoomMax = 7;

        [EditorLabel("Medium Room Min")]
        public int MediumRoomMin = 10;

        [EditorLabel("Medium Room Max")]
        public int MediumRoomMax = 15;

        [EditorLabel("Small Room Min")]
        public int SmallRoomMin = 20;

        [EditorLabel("Small Room Max")]
        public int SmallRoomMax = 25;

        [EditorLabel("Walkable Tile Min")]
        public int WalkableTileMin = 4000;

        [EditorLabel("Walkable Tile Max")]
        public int WalkableTileMax = 6000;

        [EditorLabel("Prune Dead Ends")]
        public bool PruneDeadEnds = true;

        [EditorLabel("Spawn Encounters")]
        public bool SpawnEncounters = true;

        [EditorLabel("Corridor Mob1 Spawn Denominator")]
        public int CorridorLevel1SpawnChanceDenominator = 25;

        [EditorLabel("Ante Room Monster Min")]
        public int AnteRoomMonsterMin = 1;

        [EditorLabel("Ante Room Monster Max")]
        public int AnteRoomMonsterMax = 2;

        [EditorLabel("Small Room Monster Min")]
        public int SmallRoomMonsterMin = 1;

        [EditorLabel("Small Room Monster Max")]
        public int SmallRoomMonsterMax = 2;

        [EditorLabel("Medium Room Monster Min")]
        public int MediumRoomMonsterMin = 2;

        [EditorLabel("Medium Room Monster Max")]
        public int MediumRoomMonsterMax = 4;

        [EditorLabel("Large Room Monster Min")]
        public int LargeRoomMonsterMin = 4;

        [EditorLabel("Large Room Monster Max")]
        public int LargeRoomMonsterMax = 7;
    }
}
