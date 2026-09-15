using System.Collections.Generic;
using CrystalMagic.Game.OpenField;
using Unity.Entities;
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
