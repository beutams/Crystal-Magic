using System;
using System.Collections;
using System.Collections.Generic;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;

namespace CrystalMagic.Core
{
    internal static class DungeonSceneRuntimeBuilder
    {
        private const string RuntimeRootName = "__DungeonRuntime";
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
                Debug.LogError("[DungeonSceneRuntimeBuilder] Entity spawn registry is unavailable in DungeonScene.");
                runtimeRoot.Initialize(resourceOwnerKey, spawnedEntities);
                yield break;
            }

            reportProgress?.Invoke(0.994f, "Building dungeon scene", "Spawning environment");
            SpawnEnvironment(entityManager, sceneData, resourceOwnerKey, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.996f, "Building dungeon scene", "Spawning scene objects");
            SpawnSceneObjects(entityManager, sceneData, resourceOwnerKey, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.997f, "Building dungeon scene", "Spawning player");
            SpawnPlayer(entityManager, sceneData, spawnedEntities);
            yield return null;

            reportProgress?.Invoke(0.998f, "Building dungeon scene", "Spawning monsters");
            SpawnMonsters(entityManager, sceneData, spawnedEntities);

            runtimeRoot.Initialize(resourceOwnerKey, spawnedEntities);
        }

        private static void SpawnEnvironment(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            string resourceOwnerKey,
            List<Entity> spawnedEntities)
        {
            List<RuntimeDungeonEnvironmentSpawnData> environmentSpawns = sceneData.EnvironmentSpawns;
            for (int i = 0; i < environmentSpawns.Count; i++)
            {
                RuntimeDungeonEnvironmentSpawnData spawn = environmentSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.PrefabName))
                    continue;

                if (!EntitySpawnRegistryUtility.TryInstantiateEnvironment(entityManager, new FixedString128Bytes(spawn.PrefabName), out Entity entity))
                    continue;

                SetOrAddLocalTransform(entityManager, entity, spawn.WorldPosition);
                DungeonSceneVisualUtility.ApplyEnvironmentVisual(
                    entityManager,
                    entity,
                    spawn.PrefabName,
                    spawn.MaterialPath,
                    resourceOwnerKey,
                    new float3(spawn.Size.x, spawn.Size.y, spawn.Size.z));
                ApplyBoxColliderSize(entityManager, entity, spawn.Size);
                spawnedEntities.Add(entity);
            }
        }

        private static void SpawnSceneObjects(
            EntityManager entityManager,
            RuntimeDungeonSceneData sceneData,
            string resourceOwnerKey,
            List<Entity> spawnedEntities)
        {
            List<RuntimeDungeonSceneObjectSpawnData> sceneObjects = sceneData.SceneObjects;
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                RuntimeDungeonSceneObjectSpawnData sceneObject = sceneObjects[i];
                if (sceneObject == null || string.IsNullOrWhiteSpace(sceneObject.PrefabName))
                    continue;

                if (!EntitySpawnRegistryUtility.TryInstantiateEnvironment(entityManager, new FixedString128Bytes(sceneObject.PrefabName), out Entity entity))
                    continue;

                SetOrAddLocalTransform(entityManager, entity, sceneObject.WorldPosition);
                DungeonSceneVisualUtility.ApplyEnvironmentVisual(
                    entityManager,
                    entity,
                    sceneObject.PrefabName,
                    sceneObject.ObjectType == RuntimeDungeonSceneObjectType.Exit ? sceneObject.ClosedMaterialPath : string.Empty,
                    resourceOwnerKey,
                    new float3(sceneObject.Size.x, sceneObject.Size.y, sceneObject.Size.z));
                ApplyBoxColliderSize(entityManager, entity, sceneObject.Size);
                ApplySceneObjectRuntimeData(entityManager, entity, sceneObject);
                spawnedEntities.Add(entity);
            }
        }

        private static void ApplySceneObjectRuntimeData(
            EntityManager entityManager,
            Entity entity,
            RuntimeDungeonSceneObjectSpawnData sceneObject)
        {
            switch (sceneObject.ObjectType)
            {
                case RuntimeDungeonSceneObjectType.Exit:
                    if (entityManager.HasComponent<DungeonExitComponent>(entity))
                    {
                        DungeonExitComponent exit = entityManager.GetComponentData<DungeonExitComponent>(entity);
                        exit.RegionId = sceneObject.RegionId;
                        exit.TargetFloor = Mathf.Max(1, sceneObject.TargetFloor);
                        exit.InteractionRange = Mathf.Max(0.5f, sceneObject.InteractionRange);
                        exit.RequiresRoomClear = sceneObject.RequiresRoomClear ? (byte)1 : (byte)0;
                        exit.IsOpen = 0;
                        exit.ClosedMaterialPath = new FixedString128Bytes(sceneObject.ClosedMaterialPath ?? string.Empty);
                        exit.OpenMaterialPath = new FixedString128Bytes(sceneObject.OpenMaterialPath ?? string.Empty);
                        entityManager.SetComponentData(entity, exit);
                    }
                    break;

                case RuntimeDungeonSceneObjectType.Treasure:
                    if (entityManager.HasComponent<DungeonTreasureComponent>(entity))
                    {
                        DungeonTreasureComponent treasure = entityManager.GetComponentData<DungeonTreasureComponent>(entity);
                        treasure.RegionId = sceneObject.RegionId;
                        treasure.InteractionRange = Mathf.Max(0.25f, sceneObject.InteractionRange);
                        treasure.IsOpened = 0;
                        entityManager.SetComponentData(entity, treasure);
                    }

                    if (!entityManager.HasBuffer<DungeonTreasureRewardElement>(entity))
                        entityManager.AddBuffer<DungeonTreasureRewardElement>(entity);

                    DynamicBuffer<DungeonTreasureRewardElement> rewardBuffer = entityManager.GetBuffer<DungeonTreasureRewardElement>(entity);
                    rewardBuffer.Clear();
                    if (sceneObject.Rewards != null)
                    {
                        for (int rewardIndex = 0; rewardIndex < sceneObject.Rewards.Count; rewardIndex++)
                        {
                            RuntimeDungeonTreasureRewardData reward = sceneObject.Rewards[rewardIndex];
                            if (reward == null)
                                continue;

                            rewardBuffer.Add(new DungeonTreasureRewardElement
                            {
                                RewardType = reward.RewardType,
                                ItemId = reward.ItemId,
                                Chance = reward.Chance,
                                MinQuantity = reward.MinQuantity,
                                MaxQuantity = reward.MaxQuantity,
                            });
                        }
                    }
                    break;
            }
        }

        private static void SpawnPlayer(EntityManager entityManager, RuntimeDungeonSceneData sceneData, List<Entity> spawnedEntities)
        {
            if (!TryInstantiateUnit(entityManager, PlayerPrefabName, sceneData.PlayerSpawnWorldPosition, out Entity player))
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

                if (!TryInstantiateUnit(entityManager, spawn.PrefabName, spawn.WorldPosition, out Entity monster))
                    continue;

                if (entityManager.HasComponent<DungeonMonsterSpawnComponent>(monster))
                    entityManager.SetComponentData(monster, new DungeonMonsterSpawnComponent { RegionId = spawn.RegionId });
                else
                    entityManager.AddComponentData(monster, new DungeonMonsterSpawnComponent { RegionId = spawn.RegionId });

                spawnedEntities.Add(monster);
            }
        }

        private static bool TryInstantiateUnit(EntityManager entityManager, string prefabName, Vector3 worldPosition, out Entity entity)
        {
            if (!EntitySpawnRegistryUtility.TryInstantiateUnit(entityManager, new FixedString128Bytes(prefabName), out entity))
                return false;

            SetOrAddLocalTransform(entityManager, entity, worldPosition);
            return true;
        }

        private static void SetOrAddLocalTransform(EntityManager entityManager, Entity entity, Vector3 worldPosition)
        {
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
                transform.Rotation = quaternion.identity;
                transform.Scale = 1f;
                entityManager.SetComponentData(entity, transform);
            }
            else
            {
                entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                    new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                    quaternion.identity,
                    1f));
            }
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

        private static void DestroyExistingRoot()
        {
            GameObject existing = GameObject.Find(RuntimeRootName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);
        }
    }
}
