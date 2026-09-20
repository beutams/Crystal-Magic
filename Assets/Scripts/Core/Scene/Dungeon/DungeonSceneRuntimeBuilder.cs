using System;
using System.Collections;
using System.Collections.Generic;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;

namespace CrystalMagic.Core
{
    public static class DungeonSceneRuntimeBuilder
    {
        private const string RuntimeRootName = "__DungeonRuntime";
        private const int SpawnRegistryWaitFrames = 60;
        private const string PlayerPrefabName = "PlayerDungeon";

        /// <summary>
        /// 单机入口保留原有完整行为：地图表现、静态碰撞、场景对象、玩家、兴趣点和怪物都会生成。
        /// </summary>

        public static IEnumerator BuildCurrentDungeonSceneCoroutine(
            string targetSceneName,
            Action<float, string, string> reportProgress)
        {
            DungeonRuntimeMapComponent mapData = GameRuntimeStateUtility.GetDungeonRuntimeMap();
            if (mapData == null || !mapData.HasLayout || mapData.SceneData == null)
            {
                DungeonFlowTiming.Fail("Runtime dungeon map data is unavailable");
                yield break;
            }

            DungeonFlowTiming.BeginStage(14, "准备运行时根节点并等待 ECS Spawn Registry");
            DestroyCurrentDungeonScene();

            reportProgress?.Invoke(0.985f, "Building dungeon scene", "Creating runtime scene root");
            GameObject rootObject = new(RuntimeRootName);
            DungeonSceneRuntimeRoot runtimeRoot = rootObject.AddComponent<DungeonSceneRuntimeRoot>();
            List<Entity> spawnedEntities = new();
            string resourceOwnerKey = $"{RuntimeRootName}_{Guid.NewGuid():N}";
            RuntimeDungeonSceneData sceneData = mapData.SceneData;

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

                reportProgress?.Invoke(0.992f, "Building dungeon scene", "Waiting for entity spawn registry");
                yield return null;
            }

            if (!hasSpawnRegistry)
            {
                DungeonFlowTiming.EndStage(14, "ECS Spawn Registry 不可用");
                DungeonFlowTiming.Fail("Entity spawn registry is unavailable in DungeonScene");
                Debug.LogError("[DungeonSceneRuntimeBuilder] Entity spawn registry is unavailable in DungeonScene.");
                runtimeRoot.Initialize(resourceOwnerKey, spawnedEntities);
                yield break;
            }
            DungeonFlowTiming.EndStage(14, "ECS Spawn Registry 已就绪");

            DungeonFlowTiming.BeginStage(15, "构建地牢视觉、碰撞与场景对象");
            reportProgress?.Invoke(0.993f, "Building dungeon scene", "Building tile visuals");
            DungeonRuleTileVisualBuilder.Build(runtimeRoot, sceneData.TerrainVisual, resourceOwnerKey);
            DungeonFogOfWarVisualBuilder.Build(runtimeRoot, mapData.FogData);
            runtimeRoot.SetCameraWorldBounds(sceneData.CameraWorldBounds);
            yield return null;

            reportProgress?.Invoke(0.994f, "Building dungeon scene", "Spawning obstacles");
            SpawnObstacles(entityManager, runtimeRoot, sceneData, resourceOwnerKey, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.995f, "Building dungeon scene", "Spawning environment");
            SpawnEnvironment(entityManager, sceneData, resourceOwnerKey, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.996f, "Building dungeon scene", "Spawning scene objects");
            SpawnSceneObjects(entityManager, sceneData, resourceOwnerKey, spawnedEntities);
            yield return null;
            DungeonFlowTiming.EndStage(15, "视觉、碰撞和场景对象已完成");

            DungeonFlowTiming.BeginStage(16, "生成玩家、兴趣点与怪物");
            reportProgress?.Invoke(0.997f, "Building dungeon scene", "Spawning player");
            SpawnPlayer(entityManager, sceneData, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.9975f, "Building dungeon scene", "Spawning interest point units");
            SpawnInterestPoints(entityManager, sceneData, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.998f, "Building dungeon scene", "Spawning monsters");
            SpawnMonsters(entityManager, sceneData, spawnedEntities);

            runtimeRoot.Initialize(resourceOwnerKey, spawnedEntities);
            DungeonFlowTiming.EndStage(16, $"SpawnedEntities={spawnedEntities.Count}");
        }

        /// <summary>
        /// Battle Server 只生成计算需要的静态碰撞和权威实体；不创建任何表现 GameObject。
        /// 所有会下发给客户端的实体均由 NetworkEntitySpawnUtility 创建并自动写入生成队列。
        /// </summary>
        public static bool TryBuildBattleServer(
            EntityManager entityManager,
            DungeonMapPlan mapPlan,
            IReadOnlyList<NetworkEntitySpawnInfo> playerInfos)
        {
            if (mapPlan?.sceneData == null || !HasSpawnRegistry(entityManager))
            {
                return false;
            }

            DestroyRuntimeOwnedEntities(entityManager);
            NetworkEntitySpawnUtility.EnsureSpawnQueue(entityManager);
            NetworkEntitySpawnUtility.ClearSpawnQueue(entityManager);
            List<Entity> spawnedEntities = new();
            RuntimeDungeonSceneData sceneData = mapPlan.sceneData;

            GameRuntimeStateUtility.SetDungeonRuntimeMap(
                entityManager,
                mapPlan.layout,
                sceneData,
                mapPlan.dungeonFloor,
                mapPlan.seed,
                mapPlan.attemptCount);

            SpawnObstacles(entityManager, null, sceneData, null, spawnedEntities);
            SpawnEnvironment(entityManager, sceneData, null, spawnedEntities, false);
            SpawnSceneObjects(entityManager, sceneData, null, spawnedEntities, false);

            if (playerInfos != null)
            {
                for (int index = 0; index < playerInfos.Count; index++)
                {
                    NetworkEntitySpawnInfo playerInfo = playerInfos[index];
                    if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, playerInfo, out Entity player))
                    {
                        Debug.LogError($"[DungeonSceneRuntimeBuilder] Failed to spawn Battle player '{playerInfo?.prefabName}'.");
                        DestroyRuntimeOwnedEntities(entityManager);
                        NetworkEntitySpawnUtility.ClearSpawnQueue(entityManager);
                        return false;
                    }

                    spawnedEntities.Add(player);
                }
            }

            // 兴趣点是服务器 AI 的控制实体，不向客户端同步；它们后续生成的巡逻单位会走动态 Spawn 包。
            SpawnInterestPoints(entityManager, sceneData, spawnedEntities);
            SpawnMonsters(entityManager, sceneData, spawnedEntities);

            for (int index = 0; index < spawnedEntities.Count; index++)
            {
                Entity entity = spawnedEntities[index];
                if (entity != Entity.Null && entityManager.Exists(entity) && !entityManager.HasComponent<DungeonRuntimeOwnedEntity>(entity))
                {
                    entityManager.AddComponent<DungeonRuntimeOwnedEntity>(entity);
                }
            }

            return true;
        }

        /// <summary>
        /// Battle Client 只按相同地图计划生成静态表现和碰撞。玩家、怪物、宝箱、出口等动态实体
        /// 必须等待服务器 B2C_CreateNetworkEntities 下发后统一创建。
        /// </summary>
        public static bool TryBuildBattleClient(EntityManager entityManager, DungeonMapPlan mapPlan)
        {
            if (mapPlan?.sceneData == null || !HasSpawnRegistry(entityManager))
            {
                return false;
            }

            RuntimeDungeonSceneData sceneData = mapPlan.sceneData;
            GameRuntimeStateUtility.SetDungeonRuntimeMap(
                entityManager,
                mapPlan.layout,
                sceneData,
                mapPlan.dungeonFloor,
                mapPlan.seed,
                mapPlan.attemptCount);
            GameObject rootObject = new(RuntimeRootName);
            DungeonSceneRuntimeRoot runtimeRoot = rootObject.AddComponent<DungeonSceneRuntimeRoot>();
            List<Entity> spawnedEntities = new();
            string resourceOwnerKey = $"{RuntimeRootName}_{Guid.NewGuid():N}";

            DungeonRuleTileVisualBuilder.Build(runtimeRoot, sceneData.TerrainVisual, resourceOwnerKey);
            DungeonFogOfWarVisualBuilder.Build(runtimeRoot, mapPlan.fogData);
            runtimeRoot.SetCameraWorldBounds(sceneData.CameraWorldBounds);
            SpawnObstacles(entityManager, runtimeRoot, sceneData, resourceOwnerKey, spawnedEntities);
            SpawnEnvironment(entityManager, sceneData, resourceOwnerKey, spawnedEntities, true);
            runtimeRoot.Initialize(resourceOwnerKey, spawnedEntities);
            return true;
        }

        private static void SpawnObstacles(
            EntityManager entityManager,
            DungeonSceneRuntimeRoot runtimeRoot,
            RuntimeDungeonSceneData sceneData,
            string resourceOwnerKey,
            List<Entity> spawnedEntities)
        {
            List<RuntimeDungeonObstacleSpawnData> obstacleSpawns = sceneData.ObstacleSpawns;
            for (int obstacleIndex = 0; obstacleIndex < obstacleSpawns.Count; obstacleIndex++)
            {
                RuntimeDungeonObstacleSpawnData obstacle = obstacleSpawns[obstacleIndex];
                if (obstacle == null)
                    continue;

                if (obstacle.Visuals != null)
                {
                    for (int visualIndex = 0; visualIndex < obstacle.Visuals.Count; visualIndex++)
                    {
                        RuntimeDungeonObstacleVisualSpawnData visual = obstacle.Visuals[visualIndex];
                        if (visual == null)
                            continue;

                        SpawnObstacleSpriteRenderer(runtimeRoot, visual, resourceOwnerKey, obstacleIndex, visualIndex);
                    }
                }

                if (obstacle.CollisionCells == null)
                    continue;

                for (int cellIndex = 0; cellIndex < obstacle.CollisionCells.Count; cellIndex++)
                {
                    if (!EntitySpawnRegistryUtility.TryInstantiateEnvironment(
                            entityManager,
                            new FixedString128Bytes("Collider"),
                            out Entity colliderEntity))
                    {
                        continue;
                    }

                    Vector3 colliderPosition = ToWorldCell(sceneData, obstacle.CollisionCells[cellIndex]);
                    SetOrAddLocalTransform(entityManager, colliderEntity, colliderPosition);
                    DungeonSceneVisualUtility.HideVisual(entityManager, colliderEntity);
                    ApplyBoxColliderSize(entityManager, colliderEntity, new Vector3(1f, 1f, 1.6f));
                    spawnedEntities.Add(colliderEntity);
                }
            }
        }

        private static void SpawnObstacleSpriteRenderer(
            DungeonSceneRuntimeRoot runtimeRoot,
            RuntimeDungeonObstacleVisualSpawnData visual,
            string resourceOwnerKey,
            int obstacleIndex,
            int visualIndex)
        {
            if (runtimeRoot == null || string.IsNullOrWhiteSpace(visual.SpritePath))
                return;

            string spriteReference = string.IsNullOrWhiteSpace(visual.SpriteName)
                ? visual.SpritePath
                : $"{visual.SpritePath}|{visual.SpriteName}";
            Sprite sprite = ResourceComponent.Instance?.LoadSprite(spriteReference, resourceOwnerKey);
            if (sprite == null)
                return;

            GameObject visualObject = new($"ObstacleSprite_{obstacleIndex}_{visualIndex}");
            visualObject.transform.SetParent(runtimeRoot.transform, false);
            visualObject.transform.localPosition = visual.WorldPosition;
            visualObject.transform.localRotation = Quaternion.Euler(0f, 0f, visual.RotationQuarterTurns * 90f);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = visual.FlippedX;
            renderer.sortingOrder = Mathf.RoundToInt(-visual.SortAnchorWorldY * 100f) + visual.LayerIndex;
        }

        private static void SpawnEnvironment(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            string resourceOwnerKey,
            List<Entity> spawnedEntities,
            bool createVisual = true)
        {
            List<RuntimeDungeonEnvironmentSpawnData> environmentSpawns = sceneData.EnvironmentSpawns;
            for (int i = 0; i < environmentSpawns.Count; i++)
            {
                RuntimeDungeonEnvironmentSpawnData spawn = environmentSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.PrefabName))
                    continue;

                if (!EntitySpawnRegistryUtility.TryInstantiateEnvironment(entityManager, new FixedString128Bytes(spawn.PrefabName), out Entity entity))
                    continue;

                SetOrAddLocalTransform(entityManager, entity, spawn.WorldPosition, spawn.RotationDegrees);
                if (spawn.HideVisual || !createVisual)
                {
                    DungeonSceneVisualUtility.ApplyNonUniformScale(
                        entityManager,
                        entity,
                        new float3(spawn.Size.x, spawn.Size.y, spawn.Size.z));
                    DungeonSceneVisualUtility.HideVisual(entityManager, entity);
                }
                else
                {
                    DungeonSceneVisualUtility.ApplyEnvironmentVisual(
                        entityManager,
                        entity,
                        spawn.PrefabName,
                        spawn.MaterialPath,
                        resourceOwnerKey,
                        new float3(spawn.Size.x, spawn.Size.y, spawn.Size.z));
                }
                if (spawn.ApplyCollider)
                    ApplyBoxColliderSize(entityManager, entity, spawn.Size);
                spawnedEntities.Add(entity);
            }
        }

        private static void SpawnSceneObjects(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            string resourceOwnerKey,
            List<Entity> spawnedEntities,
            bool createVisual = true)
        {
            List<RuntimeDungeonSceneObjectSpawnData> sceneObjects = sceneData.SceneObjects;
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                RuntimeDungeonSceneObjectSpawnData sceneObject = sceneObjects[i];
                if (sceneObject == null || string.IsNullOrWhiteSpace(sceneObject.PrefabName))
                    continue;

                NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                    NetworkEntityPrefabType.Environment,
                    sceneObject.PrefabName,
                    sceneObject.WorldPosition);
                entityInfo.hasScale = true;
                entityInfo.scaleX = sceneObject.Size.x;
                entityInfo.scaleY = sceneObject.Size.y;
                entityInfo.scaleZ = sceneObject.Size.z;
                entityInfo.hasCollider = true;
                entityInfo.colliderEnabled = sceneObject.ApplyCollider;
                entityInfo.colliderSizeX = sceneObject.Size.x;
                entityInfo.colliderSizeY = sceneObject.Size.y;
                entityInfo.colliderSizeZ = sceneObject.Size.z;
                if (sceneObject.ObjectType == RuntimeDungeonSceneObjectType.Exit)
                {
                    entityInfo.hasExitData = true;
                    entityInfo.exitRegionId = sceneObject.RegionId;
                    entityInfo.exitTargetThemeKey = sceneObject.TargetThemeId;
                    entityInfo.exitTargetFloor = sceneObject.TargetFloor;
                    entityInfo.exitRequiresRoomClear = sceneObject.RequiresRoomClear;
                    entityInfo.exitIsOpen = false;
                }
                else if (sceneObject.ObjectType == RuntimeDungeonSceneObjectType.Treasure)
                {
                    entityInfo.hasTreasureData = true;
                    entityInfo.treasureRegionId = sceneObject.RegionId;
                    entityInfo.treasureRandomSeed = sceneObject.RandomSeed == 0 ? 1u : sceneObject.RandomSeed;
                    entityInfo.treasureInterestSize = sceneObject.InterestSize;
                    entityInfo.treasureIsOpened = false;
                    entityInfo.treasureCandidateItemIds = sceneObject.TreasureCandidateItemIds?.ToArray();
                }
                if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity entity))
                    continue;

                if (createVisual)
                {
                    DungeonSceneVisualUtility.ApplyEnvironmentVisual(
                        entityManager,
                        entity,
                        sceneObject.PrefabName,
                        string.Empty,
                        resourceOwnerKey,
                        new float3(sceneObject.Size.x, sceneObject.Size.y, sceneObject.Size.z));
                }
                else
                {
                    DungeonSceneVisualUtility.ApplyNonUniformScale(
                        entityManager,
                        entity,
                        new float3(sceneObject.Size.x, sceneObject.Size.y, sceneObject.Size.z));
                    DungeonSceneVisualUtility.HideVisual(entityManager, entity);
                }
                spawnedEntities.Add(entity);
            }
        }

        private static void SpawnPlayer(EntityManager entityManager, RuntimeDungeonSceneData sceneData, List<Entity> spawnedEntities)
        {
            NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                NetworkEntityPrefabType.Unit,
                PlayerPrefabName,
                sceneData.PlayerSpawnWorldPosition);
            if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity player))
            {
                Debug.LogError("[DungeonSceneRuntimeBuilder] Failed to spawn PlayerDungeon.");
                return;
            }

            spawnedEntities.Add(player);
        }

        private static void SpawnMonsters(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            List<Entity> spawnedEntities)
        {
            List<RuntimeDungeonMonsterSpawnData> monsterSpawns = sceneData.MonsterSpawns;
            for (int i = 0; i < monsterSpawns.Count; i++)
            {
                RuntimeDungeonMonsterSpawnData spawn = monsterSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.PrefabName))
                    continue;

                NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                    NetworkEntityPrefabType.Unit,
                    spawn.PrefabName,
                    spawn.WorldPosition);
                spawn.SaveId = i + 1;
                entityInfo.hasMonsterSpawnData = true;
                entityInfo.monsterSaveId = spawn.SaveId;
                entityInfo.monsterRegionId = spawn.RegionId;
                entityInfo.monsterSquadId = spawn.SquadId;
                entityInfo.monsterIsBoss = spawn.IsBoss;
                if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity monster))
                    continue;

                spawnedEntities.Add(monster);
            }
        }

        private static void SpawnInterestPoints(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            List<Entity> spawnedEntities)
        {
            if (sceneData.InterestPointSpawns == null || sceneData.InterestPointSpawns.Count == 0)
                return;

            List<Entity> pointEntities = new(sceneData.InterestPointSpawns.Count);
            for (int index = 0; index < sceneData.InterestPointSpawns.Count; index++)
            {
                RuntimeDungeonInterestPointSpawnData spawn = sceneData.InterestPointSpawns[index];
                if (spawn == null)
                    continue;

                if (spawn.MemberSpawns != null)
                {
                    for (int memberIndex = 0; memberIndex < spawn.MemberSpawns.Count; memberIndex++)
                    {
                        if (spawn.MemberSpawns[memberIndex] != null)
                            spawn.MemberSpawns[memberIndex].SaveId = 1000000 + index * 10000 + memberIndex;
                    }
                }

                Entity pointEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(pointEntity, LocalTransform.FromPositionRotationScale(
                    new float3(spawn.WorldPosition.x, spawn.WorldPosition.y, spawn.WorldPosition.z),
                    quaternion.identity,
                    1f));
                entityManager.AddComponentData(pointEntity, new UnitFactionComponent
                {
                    Value = UnitFactionType.Npc,
                });
                entityManager.AddComponentData(pointEntity, new UnitVariableComponent
                {
                    Other = Entity.Null,
                });
                entityManager.AddBuffer<UnitVariableElement>(pointEntity);
                entityManager.AddBuffer<UnitVariableConsumerElement>(pointEntity);
                PopulatePatrolSpawnVariables(entityManager, pointEntity, spawn.MemberSpawns);
                entityManager.AddComponentObject(pointEntity, new UnitStateScriptComponent
                {
                    UnitDataId = DungeonPatrolRuntimeUtility.InterestPointUnitDataId,
                });
                DungeonInterestPointComponent point = new()
                {
                    EncounterId = spawn.EncounterId,
                    SquadId = spawn.SquadId,
                    SpawnDistance = Mathf.Max(0f, spawn.SpawnDistance),
                    PatrolSpeed = Mathf.Max(0f, spawn.PatrolSpeed),
                    ArrivalDistance = Mathf.Max(0.05f, spawn.ArrivalDistance),
                    PatrolEnabled = 1,
                    CurrentTarget = Entity.Null,
                    NearestPlayerDistance = float.MaxValue,
                };
                entityManager.AddComponentData(pointEntity, point);
                entityManager.AddBuffer<DungeonInterestPointCandidateElement>(pointEntity);
                DungeonPatrolRuntimeUtility.SetSharedPatrolValues(entityManager, pointEntity, point);
                entityManager.AddComponent<DungeonRuntimeOwnedEntity>(pointEntity);

                pointEntities.Add(pointEntity);
                spawnedEntities.Add(pointEntity);
            }

            for (int pointIndex = 0; pointIndex < pointEntities.Count; pointIndex++)
            {
                Entity pointEntity = pointEntities[pointIndex];
                DynamicBuffer<DungeonInterestPointCandidateElement> candidates =
                    entityManager.GetBuffer<DungeonInterestPointCandidateElement>(pointEntity);
                for (int targetIndex = 0; targetIndex < pointEntities.Count; targetIndex++)
                {
                    if (targetIndex != pointIndex)
                    {
                        candidates.Add(new DungeonInterestPointCandidateElement
                        {
                            Value = pointEntities[targetIndex],
                        });
                    }
                }
            }
        }

        private static void SetOrAddLocalTransform(
            EntityManager entityManager,
            Entity entity,
            Vector3 worldPosition,
            float rotationDegrees = 0f)
        {
            quaternion rotation = quaternion.RotateZ(math.radians(rotationDegrees));
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
                transform.Rotation = rotation;
                transform.Scale = 1f;
                entityManager.SetComponentData(entity, transform);
            }
            else
            {
                entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                    new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                    rotation,
                    1f));
            }
        }

        private static Vector3 ToWorldCell(RuntimeDungeonSceneData sceneData, Vector2Int cell)
        {
            float cellSize = sceneData.CellWorldSize > 0f
                ? sceneData.CellWorldSize
                : 1f;
            Vector2 worldOrigin = sceneData.TerrainVisual?.WorldOrigin ?? Vector2.zero;
            return new Vector3(
                worldOrigin.x + (cell.x + 0.5f) * cellSize,
                worldOrigin.y + (cell.y + 0.5f) * cellSize,
                0f);
        }

        private static void ApplyBoxColliderSize(EntityManager entityManager, Entity entity, Vector3 size)
        {
            if (!entityManager.HasComponent<PhysicsCollider>(entity))
                return;

            PhysicsCollider collider = entityManager.GetComponentData<PhysicsCollider>(entity);
            collider.Value = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(size.x, size.y, math.max(0.001f, size.z)),
                BevelRadius = 0f,
            });
            entityManager.SetComponentData(entity, collider);
        }

        private static bool HasSpawnRegistry(EntityManager entityManager)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static void PopulatePatrolSpawnVariables(
            EntityManager entityManager,
            Entity entity,
            List<RuntimeDungeonMonsterSpawnData> memberSpawns)
        {
            string listKey = DungeonPatrolRuntimeUtility.PatrolSpawnListKey;
            int count = memberSpawns?.Count ?? 0;
            UnitVariableSource.TrySetValue(entityManager, entity, $"{listKey}.count", UnitValue.FromInt(count));
            for (int index = 0; index < count; index++)
            {
                RuntimeDungeonMonsterSpawnData spawn = memberSpawns[index];
                if (spawn == null)
                    continue;

                string entryKey = $"{listKey}.{index}";
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.unit", UnitValue.FromString(spawn.PrefabName));
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.position", UnitValue.FromFloat3(new float3(
                    spawn.WorldPosition.x,
                    spawn.WorldPosition.y,
                    spawn.WorldPosition.z)));
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.hasMonsterData", UnitValue.FromBool(true));
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.monsterSaveId", UnitValue.FromInt(spawn.SaveId));
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.monsterRegionId", UnitValue.FromInt(spawn.RegionId));
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.monsterSquadId", UnitValue.FromInt(spawn.SquadId));
                UnitVariableSource.TrySetValue(entityManager, entity, $"{entryKey}.monsterIsBoss", UnitValue.FromBool(spawn.IsBoss));
            }
        }

        private static void DestroyRuntimeOwnedEntities(EntityManager entityManager)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DungeonRuntimeOwnedEntity>());
            if (!query.IsEmptyIgnoreFilter)
            {
                entityManager.DestroyEntity(query);
            }
        }

        public static void DestroyCurrentDungeonScene()
        {
            GameObject existing = GameObject.Find(RuntimeRootName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);
        }
    }
}
