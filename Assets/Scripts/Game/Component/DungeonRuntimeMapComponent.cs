using System.Collections.Generic;
using CrystalMagic.Game.OpenField;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 仅在 Dungeon 运行期间挂在 DungeonRun 实体上的本地地图表现数据。
    /// 不参与存档，也不作为游戏数值权威。
    /// </summary>
    public sealed class DungeonRuntimeMapComponent : IComponentData
    {
        public OpenFieldDungeonLayout OpenFieldLayout;
        public RuntimeDungeonSceneData SceneData;
        public RuntimeDungeonFogData FogData;
        public int Floor;
        public int Seed;
        public int AttemptCount;

        public bool HasLayout => OpenFieldLayout != null;

        public void Set(OpenFieldDungeonLayout layout, RuntimeDungeonSceneData sceneData, int floor, int seed, int attemptCount)
        {
            OpenFieldLayout = layout;
            SceneData = sceneData;
            FogData = layout != null ? new RuntimeDungeonFogData(layout, sceneData) : null;
            Floor = Mathf.Max(1, floor);
            Seed = seed;
            AttemptCount = Mathf.Max(1, attemptCount);
        }
    }

    /// <summary>
    /// Describes the immutable grid used by runtime navigation. Collision bits live in
    /// the <see cref="DungeonNavigationCollisionWord"/> buffer on the same entity.
    /// </summary>
    public struct DungeonNavigationMapComponent : IComponentData
    {
        public int Width;
        public int Height;
        public float CellSize;
        public float2 WorldOrigin;
        public int Version;

        public int CellCount => Width * Height;
    }

    /// <summary>
    /// Dense collision bitset. One buffer element stores 64 grid cells; a set bit means
    /// the corresponding cell is blocked.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DungeonNavigationCollisionWord : IBufferElementData
    {
        public ulong Value;
    }

    public static class DungeonNavigationMapUtility
    {
        public static int GetRequiredWordCount(int cellCount)
        {
            return (math.max(0, cellCount) + 63) >> 6;
        }

        public static bool IsInside(in DungeonNavigationMapComponent map, int2 cell)
        {
            return cell.x >= 0 && cell.x < map.Width && cell.y >= 0 && cell.y < map.Height;
        }

        public static int ToIndex(in DungeonNavigationMapComponent map, int2 cell)
        {
            return cell.y * map.Width + cell.x;
        }

        public static int2 ToCell(in DungeonNavigationMapComponent map, int index)
        {
            return new int2(index % map.Width, index / map.Width);
        }

        public static int2 WorldToCell(in DungeonNavigationMapComponent map, float2 worldPosition)
        {
            int2 cell = (int2)math.floor((worldPosition - map.WorldOrigin) / map.CellSize);
            return math.clamp(cell, int2.zero, new int2(map.Width - 1, map.Height - 1));
        }

        public static float2 CellToWorld(in DungeonNavigationMapComponent map, int2 cell)
        {
            return map.WorldOrigin + (new float2(cell.x, cell.y) + 0.5f) * map.CellSize;
        }

        public static bool IsBlocked(
            in DungeonNavigationMapComponent map,
            NativeArray<DungeonNavigationCollisionWord> collisionWords,
            int2 cell)
        {
            if (!IsInside(in map, cell))
                return true;

            int index = ToIndex(in map, cell);
            int wordIndex = index >> 6;
            if ((uint)wordIndex >= (uint)collisionWords.Length)
                return true;

            ulong mask = 1UL << (index & 63);
            return (collisionWords[wordIndex].Value & mask) != 0UL;
        }

        public static bool CanOccupy(
            in DungeonNavigationMapComponent map,
            NativeArray<DungeonNavigationCollisionWord> collisionWords,
            int2 cell,
            float clearanceRadius)
        {
            if (IsBlocked(in map, collisionWords, cell))
                return false;

            float radius = math.max(0f, clearanceRadius);
            if (radius <= 0f)
                return true;

            int radiusInCells = math.max(1, (int)math.ceil(radius / map.CellSize));
            float radiusSquared = radius * radius;
            for (int y = -radiusInCells; y <= radiusInCells; y++)
            {
                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    int2 checkCell = cell + new int2(x, y);
                    if (!IsBlocked(in map, collisionWords, checkCell))
                        continue;

                    float edgeX = math.max(math.abs(x) * map.CellSize - map.CellSize * 0.5f, 0f);
                    float edgeY = math.max(math.abs(y) * map.CellSize - map.CellSize * 0.5f, 0f);
                    if (edgeX * edgeX + edgeY * edgeY < radiusSquared)
                        return false;
                }
            }

            return true;
        }
    }

    public sealed class RuntimeDungeonSceneData
    {
        public float CellWorldSize;
        public Vector3 PlayerSpawnWorldPosition;
        public Rect CameraWorldBounds;
        public RuntimeDungeonTerrainVisualData TerrainVisual = new();
        public List<RuntimeDungeonObstacleSpawnData> ObstacleSpawns = new();
        public List<RuntimeDungeonEnvironmentSpawnData> EnvironmentSpawns = new();
        public List<RuntimeDungeonSceneObjectSpawnData> SceneObjects = new();
        public List<RuntimeDungeonInterestPointSpawnData> InterestPointSpawns = new();
        public List<RuntimeDungeonMonsterSpawnData> MonsterSpawns = new();
    }

    public enum RuntimeDungeonTilemapLayer
    {
        Void,
        Ground,
        Decoration,
        Obstacle,
        Boundary,
    }

    public sealed class RuntimeDungeonTerrainVisualData
    {
        public float CellWorldSize = 1f;
        public Vector2 WorldOrigin;
        public List<RuntimeDungeonRuleTilePlacement> Placements = new();
    }

    public sealed class RuntimeDungeonRuleTilePlacement
    {
        public RuntimeDungeonTilemapLayer Layer;
        public string RuleTilePath;
        public Vector2Int Cell;
    }

    public sealed class RuntimeDungeonObstacleVisualSpawnData
    {
        public string SpritePath;
        public string SpriteName;
        public Vector3 WorldPosition;
        public float SortAnchorWorldY;
        public int RotationQuarterTurns;
        public bool FlippedX;
        public int LayerIndex;
    }

    public sealed class RuntimeDungeonObstacleSpawnData
    {
        public List<RuntimeDungeonObstacleVisualSpawnData> Visuals = new();
        public List<Vector2Int> CollisionCells = new();
    }

    public sealed class RuntimeDungeonEnvironmentSpawnData
    {
        public string PrefabName;
        public string MaterialPath;
        public Vector3 WorldPosition;
        public Vector3 Size = Vector3.one;
        public float RotationDegrees;
        public bool ApplyCollider = true;
        public bool HideVisual;
        public bool IsDecoration;
    }

    public enum RuntimeDungeonSceneObjectType
    {
        Exit = 0,
        Treasure = 1,
    }

    public sealed class RuntimeDungeonSceneObjectSpawnData
    {
        public RuntimeDungeonSceneObjectType ObjectType;
        public string PrefabName;
        public int RegionId;
        public int TileIndex;
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
        public Vector3 Size = Vector3.one;
        public bool RequiresRoomClear;
        public bool ApplyCollider = true;
        public int TargetThemeId = -1;
        public int TargetFloor;
        public byte InterestSize;
        public uint RandomSeed;
        public List<int> TreasureCandidateItemIds = new();
    }

    public sealed class RuntimeDungeonMonsterSpawnData
    {
        public int SaveId = -1;
        public int RegionId;
        public int TileIndex;
        public int Level;
        public int SquadId;
        public bool IsBoss;
        public string PrefabName;
        public Vector2Int SourceCoordinate;
        public Vector2Int DisplayCoordinate;
        public Vector3 WorldPosition;
    }

    public sealed class RuntimeDungeonInterestPointSpawnData
    {
        public int EncounterId;
        public int SquadId;
        public Vector3 WorldPosition;
        public float SpawnDistance = 18f;
        public float PatrolSpeed = 3f;
        public float ArrivalDistance = 0.75f;
        public List<RuntimeDungeonMonsterSpawnData> MemberSpawns = new();
    }
}
