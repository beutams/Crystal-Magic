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
            Dictionary<Entity, int> controlledSceneObjects = new();
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
            SpawnSceneObjects(entityManager, sceneData, resourceOwnerKey, spawnedEntities, controlledSceneObjects);
            yield return null;
            DungeonFlowTiming.EndStage(15, "视觉、碰撞和场景对象已完成");

            DungeonFlowTiming.BeginStage(16, "生成玩家、兴趣点与怪物");
            reportProgress?.Invoke(0.997f, "Building dungeon scene", "Spawning player");
            SpawnPlayer(entityManager, sceneData, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.9975f, "Building dungeon scene", "Spawning interest point units");
            Entity floorController = SpawnFloorController(entityManager, sceneData, spawnedEntities);
            Dictionary<int, Entity> interestPoints = new();
            SpawnInterestPoints(entityManager, sceneData, spawnedEntities, interestPoints, floorController);
            Dictionary<int, Entity> wildSquads = SpawnWildSquads(entityManager, sceneData, spawnedEntities, floorController);
            LinkSceneObjectsToInterestPoints(entityManager, controlledSceneObjects, interestPoints);
            yield return null;

            reportProgress?.Invoke(0.998f, "Building dungeon scene", "Spawning monsters");
            SpawnMonsters(entityManager, sceneData, spawnedEntities, interestPoints, wildSquads);
            SetInterestPointsReady(entityManager, interestPoints);

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
            Dictionary<Entity, int> controlledSceneObjects = new();
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
            SpawnSceneObjects(entityManager, sceneData, null, spawnedEntities, controlledSceneObjects, false);

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
            Entity floorController = SpawnFloorController(entityManager, sceneData, spawnedEntities);
            Dictionary<int, Entity> interestPoints = new();
            SpawnInterestPoints(entityManager, sceneData, spawnedEntities, interestPoints, floorController);
            Dictionary<int, Entity> wildSquads = SpawnWildSquads(entityManager, sceneData, spawnedEntities, floorController);
            LinkSceneObjectsToInterestPoints(entityManager, controlledSceneObjects, interestPoints);
            SpawnMonsters(entityManager, sceneData, spawnedEntities, interestPoints, wildSquads);
            SetInterestPointsReady(entityManager, interestPoints);

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
            IDictionary<Entity, int> controlledSceneObjects,
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
                    entityInfo.hasDungeonExitDestination = true;
                    entityInfo.dungeonExitTargetThemeId = sceneObject.TargetThemeId;
                    entityInfo.dungeonExitTargetFloor = sceneObject.TargetFloor;
                }
                if (sceneObject.ObjectType == RuntimeDungeonSceneObjectType.Treasure)
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

                if (sceneObject.ObjectType == RuntimeDungeonSceneObjectType.Exit)
                {
                    if (entityManager.HasComponent<UnitInteractableComponent>(entity))
                    {
                        UnitInteractableComponent interactable =
                            entityManager.GetComponentData<UnitInteractableComponent>(entity);
                        interactable.IsEnabled = sceneObject.RequiresRoomClear ? (byte)0 : (byte)1;
                        interactable.NetworkDirty = 0;
                        entityManager.SetComponentData(entity, interactable);
                        entityInfo.hasInteractableData = true;
                        entityInfo.interactionKind = interactable.Data.Kind;
                        entityInfo.interactionDataId = interactable.Data.DataId;
                        entityInfo.interactionAmount = interactable.Data.Amount;
                        entityInfo.interactionVariant = interactable.Data.Variant;
                        entityInfo.interactionRangeSq = interactable.RangeSq;
                        entityInfo.interactionEnabled = interactable.IsEnabled != 0;
                    }
                    if (sceneObject.RequiresRoomClear)
                        controlledSceneObjects[entity] = sceneObject.RegionId;
                }
                else if (sceneObject.ObjectType == RuntimeDungeonSceneObjectType.Treasure)
                {
                    controlledSceneObjects[entity] = sceneObject.RegionId;
                }

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
            List<Entity> spawnedEntities,
            IReadOnlyDictionary<int, Entity> interestPoints,
            IReadOnlyDictionary<int, Entity> wildSquads)
        {
            List<RuntimeDungeonMonsterSpawnData> monsterSpawns = sceneData.MonsterSpawns;
            for (int i = 0; i < monsterSpawns.Count; i++)
            {
                RuntimeDungeonMonsterSpawnData spawn = monsterSpawns[i];
                if (spawn != null)
                    spawn.SaveId = i + 1;
                SpawnMonster(entityManager, spawn, spawnedEntities, interestPoints, false, sceneData.Difficulty);
            }

            if (sceneData.InterestPointSpawns == null)
                return;

            for (int pointIndex = 0; pointIndex < sceneData.InterestPointSpawns.Count; pointIndex++)
            {
                RuntimeDungeonInterestPointSpawnData pointSpawn = sceneData.InterestPointSpawns[pointIndex];
                if (pointSpawn?.MemberSpawns == null)
                    continue;

                for (int memberIndex = 0; memberIndex < pointSpawn.MemberSpawns.Count; memberIndex++)
                {
                    SpawnMonster(
                        entityManager,
                        pointSpawn.MemberSpawns[memberIndex],
                        spawnedEntities,
                        interestPoints,
                        false,
                        sceneData.Difficulty);
                }
            }

            if (sceneData.WildSquadSpawns == null)
                return;

            for (int squadIndex = 0; squadIndex < sceneData.WildSquadSpawns.Count; squadIndex++)
            {
                RuntimeDungeonWildSquadSpawnData squadSpawn = sceneData.WildSquadSpawns[squadIndex];
                if (squadSpawn?.MemberSpawns == null || !wildSquads.TryGetValue(squadSpawn.SquadId, out Entity squadOwner))
                    continue;

                for (int memberIndex = 0; memberIndex < squadSpawn.MemberSpawns.Count; memberIndex++)
                {
                    RuntimeDungeonMonsterSpawnData memberSpawn = squadSpawn.MemberSpawns[memberIndex];
                    if (memberSpawn != null)
                        memberSpawn.SaveId = 2000000 + squadIndex * 10000 + memberIndex;
                    Entity member = SpawnMonster(
                        entityManager,
                        memberSpawn,
                        spawnedEntities,
                        interestPoints,
                        false,
                        sceneData.Difficulty);
                    AttachWildSquadMember(entityManager, member, squadOwner);
                }
                UnitVariableSource.TrySetValue(entityManager, squadOwner, "dungeon.encounter.ready", UnitValue.FromBool(true));
            }
        }

        private static Entity SpawnMonster(
            EntityManager entityManager,
            RuntimeDungeonMonsterSpawnData spawn,
            List<Entity> spawnedEntities,
            IReadOnlyDictionary<int, Entity> interestPoints,
            bool countsAsPatrol,
            DungeonDifficultyComponent difficulty)
        {
            if (spawn == null || string.IsNullOrWhiteSpace(spawn.PrefabName))
                return Entity.Null;

            NetworkEntitySpawnInfo entityInfo = NetworkEntitySpawnUtility.CreateInfo(
                NetworkEntityPrefabType.Unit,
                spawn.PrefabName,
                spawn.WorldPosition);
            entityInfo.hasMonsterSpawnData = true;
            entityInfo.monsterSaveId = spawn.SaveId;
            entityInfo.monsterRegionId = spawn.RegionId;
            entityInfo.monsterSquadId = spawn.SquadId;
            entityInfo.monsterIsBoss = spawn.IsBoss;
            if (!NetworkEntitySpawnUtility.TrySpawn(entityManager, entityInfo, out Entity monster))
                return Entity.Null;

            difficulty.HealthApplied = 0;
            entityManager.AddComponentData(monster, difficulty);
            if (!entityManager.HasComponent<UnitSpawnInitializationComponent>(monster))
                entityManager.AddComponentData(monster, new UnitSpawnInitializationComponent { RestoreRuntimeState = 1 });

            if (interestPoints.TryGetValue(spawn.RegionId, out Entity interestPoint))
            {
                DungeonInterestPointUtility.AttachMember(
                    entityManager,
                    monster,
                    interestPoint,
                    true,
                    countsAsPatrol);
            }
            spawnedEntities.Add(monster);
            return monster;
        }

        private static void SpawnInterestPoints(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            List<Entity> spawnedEntities,
            IDictionary<int, Entity> interestPoints,
            Entity floorController)
        {
            if (sceneData.InterestPointSpawns == null || sceneData.InterestPointSpawns.Count == 0)
                return;

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
                    Value = UnitFactionType.Interactable,
                });
                entityManager.AddComponentData(pointEntity, new UnitVariableComponent
                {
                    Other = Entity.Null,
                });
                entityManager.AddBuffer<UnitVariableElement>(pointEntity);
                entityManager.AddBuffer<UnitVariableConsumerElement>(pointEntity);
                DungeonPatrolRuntimeUtility.InitializeEncounterVariables(entityManager, pointEntity, spawn.ClearThreat);
                UnitVariableSource.TrySetValue(entityManager, pointEntity, "dungeon.threat.onPatrolReturn", UnitValue.FromFloat(spawn.PatrolReturnThreat));
                UnitVariableSource.TrySetValue(entityManager, pointEntity, "dungeon.patrol.hadCombat", UnitValue.FromBool(false));
                UnitVariableSource.TrySetValue(entityManager, pointEntity, "dungeon.patrol.returningHome", UnitValue.FromBool(false));
                if (floorController != Entity.Null)
                    UnitVariableSource.SetOther(entityManager, pointEntity, floorController);
                DungeonDifficultyUtility.Inherit(entityManager, floorController, pointEntity);
                UnitVariableSource.TrySetValue(entityManager, pointEntity, "dungeon.encounter.size", UnitValue.FromInt(spawn.InterestSize));
                UnitRosterUtility.WriteTemplate(entityManager, pointEntity, "dungeon.patrol.template", spawn.PatrolTemplate);
                UnitRosterUtility.WriteTemplate(entityManager, pointEntity, "dungeon.revenge.template", spawn.RevengeTemplate);
                entityManager.AddComponentData(pointEntity, new UnitStateScriptComponent
                {
                    UnitDataId = DungeonPatrolRuntimeUtility.InterestPointUnitDataId,
                    DefinitionIndex = -1,
                });
                entityManager.AddComponent<UnitInitializationPendingTag>(pointEntity);
                entityManager.AddBuffer<StateScriptGraphStateElement>(pointEntity);
                entityManager.AddBuffer<StateScriptNodeStateElement>(pointEntity);
                entityManager.AddBuffer<StateScriptSourceCommandElement>(pointEntity);
                entityManager.AddBuffer<StateScriptSourceCommandArgumentElement>(pointEntity);
                entityManager.AddBuffer<StateScriptExternalResultElement>(pointEntity);
                DungeonInterestPointComponent point = new()
                {
                    EncounterId = spawn.EncounterId,
                    SquadId = spawn.SquadId,
                    SpawnDistance = Mathf.Max(0f, spawn.SpawnDistance),
                    PatrolSpeed = Mathf.Max(0f, spawn.PatrolSpeed),
                    ArrivalDistance = Mathf.Max(0.05f, spawn.ArrivalDistance),
                    PatrolTarget = Entity.Null,
                    PatrolEnabled = 1,
                };
                entityManager.AddComponentData(pointEntity, point);
                DungeonPatrolRuntimeUtility.SetSharedPatrolValues(entityManager, pointEntity, point);
                entityManager.AddComponent<DungeonRuntimeOwnedEntity>(pointEntity);

                spawnedEntities.Add(pointEntity);
                interestPoints[spawn.EncounterId] = pointEntity;
            }
        }

        private static Entity SpawnFloorController(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            List<Entity> spawnedEntities)
        {
            Entity controller = entityManager.CreateEntity();
            entityManager.AddComponentData(controller, LocalTransform.FromPositionRotationScale(
                new float3(sceneData.PlayerSpawnWorldPosition.x, sceneData.PlayerSpawnWorldPosition.y, sceneData.PlayerSpawnWorldPosition.z),
                quaternion.identity,
                1f));
            entityManager.AddComponentData(controller, new UnitFactionComponent { Value = UnitFactionType.Interactable });
            entityManager.AddComponentData(controller, new UnitVariableComponent { Other = Entity.Null });
            entityManager.AddBuffer<UnitVariableElement>(controller);
            entityManager.AddBuffer<UnitVariableConsumerElement>(controller);
            entityManager.AddComponentData(controller, sceneData.Difficulty);
            UnitVariableSource.TrySetValue(entityManager, controller, "dungeon.floor.number", UnitValue.FromInt(sceneData.Difficulty.Floor));
            UnitVariableSource.TrySetValue(entityManager, controller, "dungeon.floor.healthMultiplier", UnitValue.FromFloat(sceneData.Difficulty.HealthMultiplier));
            UnitVariableSource.TrySetValue(entityManager, controller, "dungeon.floor.budgetMultiplier", UnitValue.FromFloat(sceneData.Difficulty.BudgetMultiplier));
            UnitVariableSource.TrySetValue(entityManager, controller, "dungeon.floor.threat", UnitValue.FromFloat(0f));
            UnitVariableSource.TrySetValue(entityManager, controller, "dungeon.revenge.wave", UnitValue.FromInt(0));
            entityManager.AddComponentData(controller, new UnitStateScriptComponent
            {
                UnitDataId = DungeonPatrolRuntimeUtility.FloorControllerUnitDataId,
                DefinitionIndex = -1,
            });
            entityManager.AddComponent<UnitInitializationPendingTag>(controller);
            entityManager.AddBuffer<StateScriptGraphStateElement>(controller);
            entityManager.AddBuffer<StateScriptNodeStateElement>(controller);
            entityManager.AddBuffer<StateScriptSourceCommandElement>(controller);
            entityManager.AddBuffer<StateScriptSourceCommandArgumentElement>(controller);
            entityManager.AddBuffer<StateScriptExternalResultElement>(controller);
            entityManager.AddComponent<DungeonFloorControllerComponent>(controller);
            entityManager.AddComponent<DungeonRuntimeOwnedEntity>(controller);
            spawnedEntities.Add(controller);
            return controller;
        }

        private static Dictionary<int, Entity> SpawnWildSquads(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            List<Entity> spawnedEntities,
            Entity floorController)
        {
            Dictionary<int, Entity> squads = new();
            if (sceneData.WildSquadSpawns == null)
                return squads;

            for (int index = 0; index < sceneData.WildSquadSpawns.Count; index++)
            {
                RuntimeDungeonWildSquadSpawnData spawn = sceneData.WildSquadSpawns[index];
                if (spawn == null || squads.ContainsKey(spawn.SquadId))
                    continue;

                Entity squad = entityManager.CreateEntity();
                entityManager.AddComponentData(squad, LocalTransform.FromPositionRotationScale(
                    new float3(spawn.WorldPosition.x, spawn.WorldPosition.y, spawn.WorldPosition.z),
                    quaternion.identity,
                    1f));
                entityManager.AddComponentData(squad, new UnitFactionComponent { Value = UnitFactionType.Interactable });
                entityManager.AddComponentData(squad, new UnitVariableComponent { Other = Entity.Null });
                entityManager.AddBuffer<UnitVariableElement>(squad);
                entityManager.AddBuffer<UnitVariableConsumerElement>(squad);
                DungeonPatrolRuntimeUtility.InitializeEncounterVariables(entityManager, squad, spawn.ClearThreat);
                entityManager.AddComponentData(squad, new UnitStateScriptComponent
                {
                    UnitDataId = DungeonPatrolRuntimeUtility.WildSquadUnitDataId,
                    DefinitionIndex = -1,
                });
                entityManager.AddComponent<UnitInitializationPendingTag>(squad);
                entityManager.AddBuffer<StateScriptGraphStateElement>(squad);
                entityManager.AddBuffer<StateScriptNodeStateElement>(squad);
                entityManager.AddBuffer<StateScriptSourceCommandElement>(squad);
                entityManager.AddBuffer<StateScriptSourceCommandArgumentElement>(squad);
                entityManager.AddBuffer<StateScriptExternalResultElement>(squad);
                if (floorController != Entity.Null)
                    UnitVariableSource.SetOther(entityManager, squad, floorController);
                DungeonDifficultyUtility.Inherit(entityManager, floorController, squad);
                entityManager.AddComponentData(squad, new DungeonWildSquadComponent { SquadId = spawn.SquadId });
                entityManager.AddComponent<DungeonRuntimeOwnedEntity>(squad);
                spawnedEntities.Add(squad);
                squads.Add(spawn.SquadId, squad);
            }

            return squads;
        }

        private static void AttachWildSquadMember(EntityManager entityManager, Entity member, Entity squadOwner)
        {
            if (member == Entity.Null || squadOwner == Entity.Null || !entityManager.Exists(member) || !entityManager.Exists(squadOwner))
                return;

            if (!entityManager.HasComponent<UnitVariableComponent>(member))
                entityManager.AddComponentData(member, new UnitVariableComponent { Other = Entity.Null });
            if (!entityManager.HasBuffer<UnitVariableElement>(member))
                entityManager.AddBuffer<UnitVariableElement>(member);
            if (!entityManager.HasBuffer<UnitVariableConsumerElement>(member))
                entityManager.AddBuffer<UnitVariableConsumerElement>(member);
            UnitVariableSource.SetOther(entityManager, member, squadOwner);
            UnitVariableSource.TrySetValue(entityManager, member, DungeonPatrolRuntimeUtility.GuardMemberKey, UnitValue.FromBool(true));
            UnitVariableSource.TrySetValue(entityManager, squadOwner, "dungeon.encounter.populated", UnitValue.FromBool(true));

            UnitOwnerComponent owner = new() { Owner = squadOwner };
            if (entityManager.HasComponent<UnitOwnerComponent>(member))
                entityManager.SetComponentData(member, owner);
            else
                entityManager.AddComponentData(member, owner);
        }

        private static void LinkSceneObjectsToInterestPoints(
            EntityManager entityManager,
            IReadOnlyDictionary<Entity, int> controlledSceneObjects,
            IReadOnlyDictionary<int, Entity> interestPoints)
        {
            foreach (KeyValuePair<Entity, int> pair in controlledSceneObjects)
            {
                if (interestPoints.TryGetValue(pair.Value, out Entity interestPoint))
                    DungeonInterestPointUtility.AttachMember(entityManager, pair.Key, interestPoint);
            }
        }

        private static void SetInterestPointsReady(
            EntityManager entityManager,
            IReadOnlyDictionary<int, Entity> interestPoints)
        {
            foreach (KeyValuePair<int, Entity> pair in interestPoints)
            {
                DungeonInterestPointComponent point =
                    entityManager.GetComponentData<DungeonInterestPointComponent>(pair.Value);
                point.EncounterReady = 1;
                entityManager.SetComponentData(pair.Value, point);
                UnitVariableSource.TrySetValue(entityManager, pair.Value, "dungeon.encounter.ready", UnitValue.FromBool(true));
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
