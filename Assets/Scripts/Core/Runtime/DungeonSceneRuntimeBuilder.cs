using System;
using System.Collections;
using System.Collections.Generic;
using CrystalMagic.Game.MapDemo;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;
using UnityCollider = UnityEngine.Collider;
using UnityMaterial = UnityEngine.Material;
using UnityRenderer = UnityEngine.Renderer;

namespace CrystalMagic.Core
{
    internal static class DungeonSceneRuntimeBuilder
    {
        private const string RuntimeRootName = "__DungeonRuntime";
        private const float FloorVisualDepth = 0.1f;
        private const float FloorVisualZ = 0.85f;
        private const float WallVisualDepth = 1.6f;
        private const float WallVisualZ = 0.8f;
        private const float MarkerVisualSizeFactor = 0.6f;
        private const float MarkerVisualDepth = 0.5f;
        private const float MarkerVisualZ = 0.45f;
        private const float WallPhysicsDepth = 2f;
        private const int SpawnRegistryWaitFrames = 60;
        private const string PlayerPrefabName = "PlayerDungeon";

        public static IEnumerator BuildCurrentDungeonSceneCoroutine(
            string targetSceneName,
            Action<float, string, string> reportProgress)
        {
            RuntimeDungeonMapData mapData = RuntimeDataComponent.Instance.GetDungeonMapData();
            if (!mapData.HasLayout || mapData.SceneData == null)
                yield break;

            DestroyExistingRoot();

            reportProgress?.Invoke(0.985f, "Building dungeon scene", "Creating runtime scene root");

            GameObject rootObject = new(RuntimeRootName);
            DungeonSceneRuntimeRoot runtimeRoot = rootObject.AddComponent<DungeonSceneRuntimeRoot>();
            List<Entity> spawnedEntities = new();
            List<Entity> exitRoomMonsters = new();
            string resourceOwnerKey = $"{RuntimeRootName}_{Guid.NewGuid():N}";
            DungeonMakerTunnelingResult layout = mapData.Layout;
            RuntimeDungeonSceneData sceneData = mapData.SceneData;

            UnityMaterial corridorMaterial = LoadRuntimeMaterial(sceneData.CorridorMaterialPath, resourceOwnerKey, new Color(0.16f, 0.17f, 0.19f));
            UnityMaterial roomMaterial = LoadRuntimeMaterial(sceneData.RoomMaterialPath, resourceOwnerKey, new Color(0.2f, 0.21f, 0.24f));
            UnityMaterial anteRoomMaterial = LoadRuntimeMaterial(sceneData.AnteRoomMaterialPath, resourceOwnerKey, new Color(0.19f, 0.2f, 0.17f));
            UnityMaterial wallMaterial = LoadRuntimeMaterial(sceneData.WallMaterialPath, resourceOwnerKey, new Color(0.08f, 0.09f, 0.11f));
            UnityMaterial startMaterial = LoadRuntimeMaterial(sceneData.StartMarkerMaterialPath, resourceOwnerKey, new Color(0.22f, 0.68f, 0.34f));
            UnityMaterial exitClosedMaterial = LoadRuntimeMaterial(sceneData.ExitClosedMaterialPath, resourceOwnerKey, new Color(0.76f, 0.29f, 0.24f));
            UnityMaterial exitOpenMaterial = LoadRuntimeMaterial(sceneData.ExitOpenMaterialPath, resourceOwnerKey, new Color(0.24f, 0.72f, 0.94f));
            List<UnityMaterial> trackedMaterials = new()
            {
                corridorMaterial,
                roomMaterial,
                anteRoomMaterial,
                wallMaterial,
                startMaterial,
                exitClosedMaterial,
                exitOpenMaterial,
            };

            reportProgress?.Invoke(0.988f, "Building dungeon scene", "Generating floor and wall geometry");
            CreateFloorVisuals(rootObject.transform, layout, sceneData.CellWorldSize, corridorMaterial, roomMaterial, anteRoomMaterial);
            List<RectInt> wallRectangles = CreateWallVisuals(rootObject.transform, layout, sceneData.CellWorldSize, wallMaterial);
            yield return null;

            reportProgress?.Invoke(0.992f, "Building dungeon scene", "Creating wall collision");
            CreateWallPhysics(wallRectangles, layout, sceneData.CellWorldSize, spawnedEntities);
            GameObject startObject = CreateMarkerObject(
                "DungeonStart",
                rootObject.transform,
                sceneData.StartObject?.WorldPosition ?? Vector3.zero,
                sceneData.CellWorldSize,
                startMaterial);
            GameObject exitObject = CreateMarkerObject(
                "DungeonExit",
                rootObject.transform,
                sceneData.NextLevelEntranceObject?.WorldPosition ?? Vector3.zero,
                sceneData.CellWorldSize,
                exitClosedMaterial);
            yield return null;

            EntityManager entityManager = default;
            bool hasSpawnRegistry = false;
            for (int frame = 0; frame < SpawnRegistryWaitFrames; frame++)
            {
                World world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    entityManager = world.EntityManager;
                    if (HasSpawnRegistry(entityManager))
                    {
                        hasSpawnRegistry = true;
                        break;
                    }
                }

                reportProgress?.Invoke(0.994f, "Building dungeon scene", "Waiting for entity spawn registry");
                yield return null;
            }

            if (!hasSpawnRegistry)
            {
                Debug.LogError("[DungeonSceneRuntimeBuilder] Entity spawn registry is unavailable in DungeonScene.");
                runtimeRoot.Initialize(
                    mapData.Floor,
                    sceneData.NextLevelEntranceObject,
                    exitObject != null ? exitObject.GetComponent<UnityRenderer>() : null,
                    exitClosedMaterial,
                    exitOpenMaterial,
                    sceneData.ExitInteractionRange,
                    resourceOwnerKey,
                    trackedMaterials,
                    spawnedEntities,
                    exitRoomMonsters);
                yield break;
            }

            reportProgress?.Invoke(0.996f, "Building dungeon scene", "Spawning player");
            SpawnPlayer(entityManager, sceneData, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.998f, "Building dungeon scene", "Spawning monsters");
            SpawnMonsters(entityManager, layout, sceneData, spawnedEntities, exitRoomMonsters);

            runtimeRoot.Initialize(
                mapData.Floor,
                sceneData.NextLevelEntranceObject,
                exitObject != null ? exitObject.GetComponent<UnityRenderer>() : null,
                exitClosedMaterial,
                exitOpenMaterial,
                sceneData.ExitInteractionRange,
                resourceOwnerKey,
                trackedMaterials,
                spawnedEntities,
                exitRoomMonsters);

            if (startObject == null)
                Debug.LogWarning("[DungeonSceneRuntimeBuilder] Failed to create dungeon start marker.");
        }

        private static void SpawnPlayer(EntityManager entityManager, RuntimeDungeonSceneData sceneData, List<Entity> spawnedEntities)
        {
            if (!TryInstantiateUnit(entityManager, PlayerPrefabName, sceneData.StartObject?.WorldPosition ?? Vector3.zero, out Entity player))
            {
                Debug.LogError("[DungeonSceneRuntimeBuilder] Failed to spawn PlayerDungeon.");
                return;
            }

            spawnedEntities.Add(player);
        }

        private static void SpawnMonsters(
            EntityManager entityManager,
            DungeonMakerTunnelingResult layout,
            RuntimeDungeonSceneData sceneData,
            List<Entity> spawnedEntities,
            List<Entity> exitRoomMonsters)
        {
            int exitRegionId = sceneData.NextLevelEntranceObject?.RegionId ?? -1;
            List<RuntimeDungeonMonsterSpawnData> monsterSpawns = sceneData.MonsterSpawns;
            for (int i = 0; i < monsterSpawns.Count; i++)
            {
                RuntimeDungeonMonsterSpawnData spawn = monsterSpawns[i];
                if (string.IsNullOrWhiteSpace(spawn.PrefabName))
                    continue;

                if (!TryInstantiateUnit(entityManager, spawn.PrefabName, spawn.WorldPosition, out Entity monster))
                    continue;

                spawnedEntities.Add(monster);
                if (spawn.RegionId == exitRegionId)
                    exitRoomMonsters.Add(monster);
            }
        }

        private static bool TryInstantiateUnit(EntityManager entityManager, string prefabName, Vector3 worldPosition, out Entity entity)
        {
            if (!EntitySpawnRegistryUtility.TryInstantiateUnit(entityManager, new FixedString128Bytes(prefabName), out entity))
                return false;

            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = new float3(worldPosition.x, worldPosition.y, 0f);
                transform.Rotation = quaternion.identity;
                entityManager.SetComponentData(entity, transform);
            }
            else
            {
                entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                    new float3(worldPosition.x, worldPosition.y, 0f),
                    quaternion.identity,
                    1f));
            }

            return true;
        }

        private static void CreateWallPhysics(
            IReadOnlyList<RectInt> wallRectangles,
            DungeonMakerTunnelingResult layout,
            float cellWorldSize,
            List<Entity> spawnedEntities)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            for (int i = 0; i < wallRectangles.Count; i++)
            {
                RectInt rectangle = wallRectangles[i];
                Vector3 center = GetWorldPositionForRectangle(rectangle, layout.DisplayWidth, layout.DisplayHeight, cellWorldSize, 0f);
                float3 size = new float3(
                    rectangle.width * cellWorldSize,
                    rectangle.height * cellWorldSize,
                    WallPhysicsDepth);

                Entity wallEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(wallEntity, LocalTransform.FromPositionRotationScale(center, quaternion.identity, 1f));
                entityManager.AddComponentData(wallEntity, new PhysicsCollider
                {
                    Value = BoxCollider.Create(new BoxGeometry
                    {
                        Center = float3.zero,
                        Orientation = quaternion.identity,
                        Size = size,
                        BevelRadius = 0f,
                    })
                });
                spawnedEntities.Add(wallEntity);
            }
        }

        private static void CreateFloorVisuals(
            Transform parent,
            DungeonMakerTunnelingResult layout,
            float cellWorldSize,
            UnityMaterial corridorMaterial,
            UnityMaterial roomMaterial,
            UnityMaterial anteRoomMaterial)
        {
            GameObject floorsRoot = new("FloorRectangles");
            floorsRoot.transform.SetParent(parent, false);

            DungeonMakerRegionKind?[,] regionKinds = BuildRegionKindMap(layout);
            CreateFloorRectsForMask(
                "RoomFloorRect",
                floorsRoot.transform,
                layout,
                cellWorldSize,
                roomMaterial,
                BuildWalkableMask(layout, regionKinds, DungeonMakerRegionKind.Room));
            CreateFloorRectsForMask(
                "AnteRoomFloorRect",
                floorsRoot.transform,
                layout,
                cellWorldSize,
                anteRoomMaterial,
                BuildWalkableMask(layout, regionKinds, DungeonMakerRegionKind.AnteRoom));
            CreateFloorRectsForMask(
                "CorridorFloorRect",
                floorsRoot.transform,
                layout,
                cellWorldSize,
                corridorMaterial,
                BuildWalkableMask(layout, regionKinds, DungeonMakerRegionKind.Corridor));
        }

        private static List<RectInt> CreateWallVisuals(Transform parent, DungeonMakerTunnelingResult layout, float cellWorldSize, UnityMaterial wallMaterial)
        {
            GameObject wallsRoot = new("WallRectangles");
            wallsRoot.transform.SetParent(parent, false);

            List<RectInt> wallRectangles = BuildRectangles(
                layout,
                static tile => IsWallTile(tile),
                surfaceOnly: true);

            for (int i = 0; i < wallRectangles.Count; i++)
            {
                RectInt rectangle = wallRectangles[i];
                CreateRectVisual(
                    $"WallRect_{i:D3}",
                    wallsRoot.transform,
                    rectangle,
                    layout.DisplayWidth,
                    layout.DisplayHeight,
                    cellWorldSize,
                    WallVisualDepth,
                    WallVisualZ,
                    wallMaterial);
            }

            return wallRectangles;
        }

        private static GameObject CreateMarkerObject(
            string objectName,
            Transform parent,
            Vector3 worldPosition,
            float cellWorldSize,
            UnityMaterial material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = objectName;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(worldPosition.x, worldPosition.y, MarkerVisualZ);
            float markerSize = Mathf.Max(0.5f, cellWorldSize * MarkerVisualSizeFactor);
            marker.transform.localScale = new Vector3(markerSize, markerSize, MarkerVisualDepth);

            UnityCollider visualCollider = marker.GetComponent<UnityCollider>();
            if (visualCollider != null)
                UnityEngine.Object.Destroy(visualCollider);

            UnityRenderer renderer = marker.GetComponent<UnityRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return marker;
        }

        private static void CreateRectVisual(
            string objectName,
            Transform parent,
            RectInt rectangle,
            int displayWidth,
            int displayHeight,
            float cellWorldSize,
            float depth,
            float visualZ,
            UnityMaterial material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = GetWorldPositionForRectangle(rectangle, displayWidth, displayHeight, cellWorldSize, visualZ);
            cube.transform.localScale = new Vector3(
                rectangle.width * cellWorldSize,
                rectangle.height * cellWorldSize,
                depth);

            UnityCollider visualCollider = cube.GetComponent<UnityCollider>();
            if (visualCollider != null)
                UnityEngine.Object.Destroy(visualCollider);

            UnityRenderer renderer = cube.GetComponent<UnityRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static Vector3 GetWorldPositionForRectangle(
            RectInt rectangle,
            int displayWidth,
            int displayHeight,
            float cellWorldSize,
            float z)
        {
            float halfWidth = displayWidth * 0.5f;
            float halfHeight = displayHeight * 0.5f;
            float centerX = (rectangle.x + rectangle.width * 0.5f - halfWidth) * cellWorldSize;
            float centerY = (rectangle.y + rectangle.height * 0.5f - halfHeight) * cellWorldSize;
            return new Vector3(centerX, centerY, z);
        }

        private static void CreateFloorRectsForMask(
            string prefix,
            Transform parent,
            DungeonMakerTunnelingResult layout,
            float cellWorldSize,
            UnityMaterial material,
            bool[,] mask)
        {
            List<RectInt> rectangles = BuildRectangles(mask, surfaceOnly: false);
            for (int i = 0; i < rectangles.Count; i++)
            {
                CreateRectVisual(
                    $"{prefix}_{i:D3}",
                    parent,
                    rectangles[i],
                    layout.DisplayWidth,
                    layout.DisplayHeight,
                    cellWorldSize,
                    FloorVisualDepth,
                    FloorVisualZ,
                    material);
            }
        }

        private static DungeonMakerRegionKind?[,] BuildRegionKindMap(DungeonMakerTunnelingResult layout)
        {
            int sourceWidth = layout.SourceWidth;
            int sourceHeight = layout.SourceHeight;
            DungeonMakerRegionKind?[,] map = new DungeonMakerRegionKind?[layout.DisplayWidth, layout.DisplayHeight];
            IReadOnlyList<DungeonMakerRegion> regions = layout.Regions;

            for (int i = 0; i < regions.Count; i++)
            {
                DungeonMakerRegion region = regions[i];
                if (region?.TileIndices == null)
                    continue;

                for (int tileIndexIndex = 0; tileIndexIndex < region.TileIndices.Length; tileIndexIndex++)
                {
                    int tileIndex = region.TileIndices[tileIndexIndex];
                    int sourceX = tileIndex / sourceHeight;
                    int sourceY = tileIndex % sourceHeight;
                    if (sourceX < 0 || sourceX >= sourceWidth || sourceY < 0 || sourceY >= sourceHeight)
                        continue;

                    map[sourceY, sourceX] = region.Kind;
                }
            }

            return map;
        }

        private static bool[,] BuildWalkableMask(
            DungeonMakerTunnelingResult layout,
            DungeonMakerRegionKind?[,] regionKinds,
            DungeonMakerRegionKind targetKind)
        {
            int width = layout.DisplayWidth;
            int height = layout.DisplayHeight;
            bool[,] mask = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsWalkableTile(layout.GetDisplayTile(x, y)))
                        continue;

                    DungeonMakerRegionKind resolvedKind = regionKinds[x, y] ?? DungeonMakerRegionKind.Corridor;
                    mask[x, y] = resolvedKind == targetKind;
                }
            }

            return mask;
        }

        private static List<RectInt> BuildRectangles(
            DungeonMakerTunnelingResult layout,
            Func<DungeonMakerSquareData, bool> predicate,
            bool surfaceOnly)
        {
            int width = layout.DisplayWidth;
            int height = layout.DisplayHeight;
            bool[,] targetMask = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    targetMask[x, y] = predicate(layout.GetDisplayTile(x, y));
            }

            return BuildRectangles(targetMask, surfaceOnly);
        }

        private static List<RectInt> BuildRectangles(bool[,] targetMask, bool surfaceOnly)
        {
            int width = targetMask.GetLength(0);
            int height = targetMask.GetLength(1);
            bool[,] used = new bool[width, height];
            List<RectInt> rectangles = new();

            if (surfaceOnly)
            {
                bool[,] sourceMask = targetMask;
                targetMask = new bool[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        targetMask[x, y] = IsSurfaceMask(sourceMask, x, y, width, height);
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!targetMask[x, y] || used[x, y])
                        continue;

                    int rectWidth = 0;
                    while (x + rectWidth < width && targetMask[x + rectWidth, y] && !used[x + rectWidth, y])
                        rectWidth++;

                    int rectHeight = 1;
                    bool canGrow = true;
                    while (y + rectHeight < height && canGrow)
                    {
                        for (int dx = 0; dx < rectWidth; dx++)
                        {
                            if (!targetMask[x + dx, y + rectHeight] || used[x + dx, y + rectHeight])
                            {
                                canGrow = false;
                                break;
                            }
                        }

                        if (canGrow)
                            rectHeight++;
                    }

                    for (int dy = 0; dy < rectHeight; dy++)
                    {
                        for (int dx = 0; dx < rectWidth; dx++)
                            used[x + dx, y + dy] = true;
                    }

                    rectangles.Add(new RectInt(x, y, rectWidth, rectHeight));
                }
            }

            return rectangles;
        }

        private static bool IsSurfaceMask(bool[,] mask, int x, int y, int width, int height)
        {
            if (!mask[x, y])
                return false;

            if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                return true;

            return !mask[x - 1, y]
                || !mask[x + 1, y]
                || !mask[x, y - 1]
                || !mask[x, y + 1];
        }

        private static bool IsWallTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.CLOSED
                or DungeonMakerSquareData.G_CLOSED
                or DungeonMakerSquareData.NJ_CLOSED
                or DungeonMakerSquareData.NJ_G_CLOSED
                or DungeonMakerSquareData.COLUMN;
        }

        private static bool IsWalkableTile(DungeonMakerSquareData tile)
        {
            return tile is DungeonMakerSquareData.OPEN
                or DungeonMakerSquareData.G_OPEN
                or DungeonMakerSquareData.NJ_OPEN
                or DungeonMakerSquareData.NJ_G_OPEN
                or DungeonMakerSquareData.IR_OPEN
                or DungeonMakerSquareData.IT_OPEN
                or DungeonMakerSquareData.IA_OPEN
                or DungeonMakerSquareData.H_DOOR
                or DungeonMakerSquareData.V_DOOR
                or DungeonMakerSquareData.MOB1
                or DungeonMakerSquareData.MOB2
                or DungeonMakerSquareData.MOB3
                or DungeonMakerSquareData.TREAS1
                or DungeonMakerSquareData.TREAS2
                or DungeonMakerSquareData.TREAS3;
        }

        private static UnityMaterial LoadRuntimeMaterial(string path, string ownerKey, Color fallbackColor)
        {
            UnityMaterial assetMaterial = !string.IsNullOrWhiteSpace(path)
                ? ResourceComponent.Instance?.Load<UnityMaterial>(path, ownerKey)
                : null;
            if (assetMaterial != null)
                return new UnityMaterial(assetMaterial);

            return CreateMaterial(fallbackColor);
        }

        private static UnityMaterial CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            UnityMaterial material = new(shader)
            {
                color = color,
            };
            return material;
        }

        private static bool HasSpawnRegistry(EntityManager entityManager)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static void DestroyExistingRoot()
        {
            GameObject existing = GameObject.Find(RuntimeRootName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);
        }
    }
}
