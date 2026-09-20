using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Server
{
    /// <summary>
    /// 所有同步实体唯一的生成入口。
    /// Server/Client World 会添加网络身份，Battle Server World 存在生成队列时还会自动记录实体信息；
    /// Standalone World 只复用实体生成和初始化逻辑，不添加任何网络组件。
    /// </summary>
    public static class NetworkEntitySpawnUtility
    {
        public static NetworkEntitySpawnInfo CreateInfo(
            NetworkEntityPrefabType prefabType,
            string prefabName,
            Vector3 position,
            ulong ownerAccountId = 0UL)
        {
            return new NetworkEntitySpawnInfo
            {
                unitId = Guid.NewGuid(),
                prefabType = prefabType,
                prefabName = prefabName,
                ownerAccountId = ownerAccountId,
                x = position.x,
                y = position.y,
                z = position.z,
            };
        }

        public static bool TrySpawn(EntityManager entityManager, NetworkEntitySpawnInfo entityInfo, out Entity entity)
        {
            entity = Entity.Null;
            if (entityInfo == null || entityInfo.unitId == Guid.Empty || string.IsNullOrWhiteSpace(entityInfo.prefabName))
            {
                return false;
            }

            FixedString128Bytes prefabName = new(entityInfo.prefabName);
            bool instantiated = entityInfo.prefabType switch
            {
                NetworkEntityPrefabType.Unit => EntitySpawnRegistryUtility.TryInstantiateUnit(entityManager, prefabName, out entity),
                NetworkEntityPrefabType.Environment => EntitySpawnRegistryUtility.TryInstantiateEnvironment(entityManager, prefabName, out entity),
                NetworkEntityPrefabType.Drop => EntitySpawnRegistryUtility.TryInstantiateDrop(entityManager, prefabName, out entity),
                NetworkEntityPrefabType.Projectile => EntitySpawnRegistryUtility.TryInstantiateProjectile(entityManager, prefabName, out entity),
                _ => false,
            };
            if (!instantiated)
            {
                entity = Entity.Null;
                return false;
            }

            SetOrAddLocalTransform(entityManager, entity, new Vector3(entityInfo.x, entityInfo.y, entityInfo.z));
            ApplySpatialData(entityManager, entity, entityInfo);
            WorldFlags worldFlags = entityManager.WorldUnmanaged.Flags;
            bool isNetworkWorld =
                (worldFlags & WorldFlags.GameServer) == WorldFlags.GameServer ||
                (worldFlags & WorldFlags.GameClient) == WorldFlags.GameClient;
            if (isNetworkWorld)
            {
                SetOrAddComponent(entityManager, entity, new NetworkIdentityComponent { id = entityInfo.unitId });
                SetOrAddSpawnInfo(entityManager, entity, entityInfo);
            }
            if (!entityManager.HasComponent<DungeonRuntimeOwnedEntity>(entity))
            {
                entityManager.AddComponent<DungeonRuntimeOwnedEntity>(entity);
            }
            ApplyInitialState(entityManager, entity, entityInfo);
            if ((worldFlags & WorldFlags.GameClient) == WorldFlags.GameClient &&
                entityManager.HasComponent<PlayerInputComponent>(entity))
            {
                entityManager.RemoveComponent<PlayerInputComponent>(entity);
            }
            if (isNetworkWorld)
                EnqueueSpawnInfo(entityManager, entityInfo);
            return true;
        }

        public static void EnsureSpawnQueue(EntityManager entityManager)
        {
            if (TryGetSpawnQueue(entityManager, out _))
            {
                return;
            }

            Entity queueEntity = entityManager.CreateEntity();
            entityManager.AddComponentObject(queueEntity, new NetworkEntitySpawnQueueComponent());
        }

        public static void ClearSpawnQueue(EntityManager entityManager)
        {
            if (TryGetSpawnQueue(entityManager, out NetworkEntitySpawnQueueComponent spawnQueue))
            {
                spawnQueue.entityInfos.Clear();
            }
        }

        public static NetworkEntitySpawnInfo[] TakeSpawnQueue(EntityManager entityManager)
        {
            if (!TryGetSpawnQueue(entityManager, out NetworkEntitySpawnQueueComponent spawnQueue) ||
                spawnQueue.entityInfos.Count == 0)
            {
                return Array.Empty<NetworkEntitySpawnInfo>();
            }

            NetworkEntitySpawnInfo[] entityInfos = spawnQueue.entityInfos.ToArray();
            spawnQueue.entityInfos.Clear();
            return entityInfos;
        }

        public static NetworkEntitySpawnInfo[] CreateSnapshotInfos(EntityManager entityManager)
        {
            EntityQuery query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkIdentityComponent>(),
                ComponentType.ReadOnly<NetworkEntitySpawnInfoComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            List<NetworkEntitySpawnInfo> entityInfos = new();
            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                if (entityManager.HasComponent<DestroyEntityFlag>(entity) &&
                    entityManager.IsComponentEnabled<DestroyEntityFlag>(entity))
                {
                    continue;
                }

                NetworkEntitySpawnInfoComponent source = entityManager.GetComponentObject<NetworkEntitySpawnInfoComponent>(entity);
                if (source?.entityInfo == null)
                {
                    continue;
                }

                NetworkEntitySpawnInfo entityInfo = CloneInfo(source.entityInfo);
                entityInfo.unitId = entityManager.GetComponentData<NetworkIdentityComponent>(entity).id;
                if (entityInfo.unitId == Guid.Empty)
                {
                    continue;
                }

                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                    entityInfo.x = transform.Position.x;
                    entityInfo.y = transform.Position.y;
                    entityInfo.z = transform.Position.z;
                }

                if (entityManager.HasComponent<PlayerCharacterComponent>(entity))
                {
                    entityInfo.characterData = entityManager.GetComponentObject<PlayerCharacterComponent>(entity).Data;
                }

                if (entityManager.HasComponent<UnitFactionComponent>(entity))
                {
                    entityInfo.hasFaction = true;
                    entityInfo.faction = entityManager.GetComponentData<UnitFactionComponent>(entity).Value;
                }

                if (entityManager.HasComponent<UnitVitalityComponent>(entity))
                {
                    entityInfo.hasHealth = true;
                    entityInfo.health = entityManager.GetComponentData<UnitVitalityComponent>(entity).CurrentHealth;
                }

                if (entityManager.HasComponent<UnitManaComponent>(entity))
                {
                    entityInfo.hasMana = true;
                    entityInfo.mana = entityManager.GetComponentData<UnitManaComponent>(entity).CurrentMana;
                }

                if (entityManager.HasComponent<UnitInteractableComponent>(entity))
                {
                    UnitInteractableComponent interactable = entityManager.GetComponentData<UnitInteractableComponent>(entity);
                    entityInfo.hasInteractableData = true;
                    entityInfo.interactionKind = interactable.Data.Kind;
                    entityInfo.interactionDataId = interactable.Data.DataId;
                    entityInfo.interactionAmount = interactable.Data.Amount;
                    entityInfo.interactionVariant = interactable.Data.Variant;
                    entityInfo.interactionRangeSq = interactable.RangeSq;
                    entityInfo.interactionEnabled = interactable.IsEnabled != 0;
                }

                if (entityManager.HasComponent<DungeonExitComponent>(entity))
                {
                    DungeonExitComponent exit = entityManager.GetComponentData<DungeonExitComponent>(entity);
                    entityInfo.hasExitData = true;
                    entityInfo.exitRegionId = exit.RegionId;
                    entityInfo.exitTargetThemeKey = exit.TargetThemeId;
                    entityInfo.exitTargetFloor = exit.TargetFloor;
                    entityInfo.exitRequiresRoomClear = exit.RequiresRoomClear != 0;
                    entityInfo.exitIsOpen = exit.IsOpen != 0;
                }

                if (entityManager.HasComponent<TreasureComponent>(entity))
                {
                    TreasureComponent treasure = entityManager.GetComponentData<TreasureComponent>(entity);
                    entityInfo.hasTreasureData = true;
                    entityInfo.treasureRegionId = treasure.RegionId;
                    entityInfo.treasureRandomSeed = treasure.RandomSeed;
                    entityInfo.treasureInterestSize = treasure.InterestSize;
                    entityInfo.treasureIsOpened = treasure.IsOpened != 0;
                    if (entityManager.HasBuffer<DungeonTreasureCandidateItemElement>(entity))
                    {
                        DynamicBuffer<DungeonTreasureCandidateItemElement> candidateBuffer =
                            entityManager.GetBuffer<DungeonTreasureCandidateItemElement>(entity);
                        entityInfo.treasureCandidateItemIds = new int[candidateBuffer.Length];
                        for (int itemIndex = 0; itemIndex < candidateBuffer.Length; itemIndex++)
                        {
                            entityInfo.treasureCandidateItemIds[itemIndex] = candidateBuffer[itemIndex].ItemId;
                        }
                    }
                }

                entityInfos.Add(entityInfo);
            }

            return entityInfos.ToArray();
        }

        public static bool TryFindEntity(EntityManager entityManager, Guid unitId, out Entity entity)
        {
            entity = Entity.Null;
            if (unitId == Guid.Empty)
            {
                return false;
            }

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkIdentityComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int index = 0; index < entities.Length; index++)
            {
                Entity candidate = entities[index];
                if (entityManager.GetComponentData<NetworkIdentityComponent>(candidate).id == unitId)
                {
                    entity = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void ApplyInitialState(EntityManager entityManager, Entity entity, NetworkEntitySpawnInfo entityInfo)
        {
            if (entityInfo.characterData != null)
            {
                entityInfo.characterData.Equipment ??= new EquipmentData();
                EquipmentUtility.EnsureValid(entityInfo.characterData.Equipment);
                EquipmentUtility.RebuildProperties(entityInfo.characterData.Equipment);
                if (entityManager.HasComponent<PlayerCharacterComponent>(entity))
                {
                    entityManager.GetComponentObject<PlayerCharacterComponent>(entity).Data = entityInfo.characterData;
                }
                else
                {
                    entityManager.AddComponentObject(entity, new PlayerCharacterComponent { Data = entityInfo.characterData });
                }

                EquipmentUtility.ApplyToUnit(entityManager, entity, entityInfo.characterData.Equipment);
                PlayerSkillRuntimeDataUtility.Initialize(entityManager, entity, entityInfo.characterData);
            }

            if (entityInfo.hasFaction)
            {
                SetOrAddComponent(entityManager, entity, new UnitFactionComponent
                {
                    Value = entityInfo.faction,
                });
            }

            if (entityInfo.hasHealth && entityManager.HasComponent<UnitVitalityComponent>(entity))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                vitality.CurrentHealth = entityInfo.health;
                entityManager.SetComponentData(entity, vitality);
            }

            if (entityInfo.hasMana && entityManager.HasComponent<UnitManaComponent>(entity))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
                mana.CurrentMana = entityInfo.mana;
                entityManager.SetComponentData(entity, mana);
            }

            if (entityInfo.hasMonsterSpawnData)
            {
                SetOrAddComponent(entityManager, entity, new DungeonMonsterSpawnComponent
                {
                    SaveId = entityInfo.monsterSaveId,
                    RegionId = entityInfo.monsterRegionId,
                    SquadId = entityInfo.monsterSquadId,
                    IsBoss = entityInfo.monsterIsBoss ? (byte)1 : (byte)0,
                });
            }

            if (entityInfo.hasExitData)
            {
                SetOrAddComponent(entityManager, entity, new DungeonExitComponent
                {
                    RegionId = entityInfo.exitRegionId,
                    TargetThemeId = entityInfo.exitTargetThemeKey,
                    TargetFloor = Mathf.Max(1, entityInfo.exitTargetFloor),
                    RequiresRoomClear = entityInfo.exitRequiresRoomClear ? (byte)1 : (byte)0,
                    IsOpen = entityInfo.exitIsOpen ? (byte)1 : (byte)0,
                });
            }

            if (entityInfo.hasTreasureData)
            {
                SetOrAddComponent(entityManager, entity, new TreasureComponent
                {
                    RegionId = entityInfo.treasureRegionId,
                    RandomSeed = entityInfo.treasureRandomSeed == 0 ? 1u : entityInfo.treasureRandomSeed,
                    InterestSize = entityInfo.treasureInterestSize,
                    IsOpened = entityInfo.treasureIsOpened ? (byte)1 : (byte)0,
                });
                SetOrAddComponent(entityManager, entity, new UnitInteractableComponent
                {
                    Data = new UnitInteractionData
                    {
                        Kind = InteractionKind.Treasure,
                        DataId = entityInfo.treasureRegionId,
                    },
                    RangeSq = -1f,
                    IsEnabled = entityInfo.treasureIsOpened ? (byte)0 : (byte)1,
                });
                if (!entityManager.HasBuffer<DungeonTreasureCandidateItemElement>(entity))
                {
                    entityManager.AddBuffer<DungeonTreasureCandidateItemElement>(entity);
                }

                DynamicBuffer<DungeonTreasureCandidateItemElement> candidateBuffer = entityManager.GetBuffer<DungeonTreasureCandidateItemElement>(entity);
                candidateBuffer.Clear();
                if (entityInfo.treasureCandidateItemIds != null)
                {
                    foreach (int itemId in entityInfo.treasureCandidateItemIds)
                    {
                        if (itemId >= 0)
                        {
                            candidateBuffer.Add(new DungeonTreasureCandidateItemElement { ItemId = itemId });
                        }
                    }
                }
            }

            if (entityInfo.hasInteractableData)
            {
                SetOrAddComponent(entityManager, entity, new UnitInteractableComponent
                {
                    Data = new UnitInteractionData
                    {
                        Kind = entityInfo.interactionKind,
                        DataId = entityInfo.interactionDataId,
                        Amount = entityInfo.interactionAmount,
                        Variant = entityInfo.interactionVariant,
                    },
                    RangeSq = entityInfo.interactionRangeSq,
                    IsEnabled = entityInfo.interactionEnabled ? (byte)1 : (byte)0,
                });
            }
        }

        private static void EnqueueSpawnInfo(EntityManager entityManager, NetworkEntitySpawnInfo entityInfo)
        {
            if (TryGetSpawnQueue(entityManager, out NetworkEntitySpawnQueueComponent spawnQueue))
            {
                spawnQueue.entityInfos.Add(entityInfo);
            }
        }

        private static NetworkEntitySpawnInfo CloneInfo(NetworkEntitySpawnInfo source)
        {
            return new NetworkEntitySpawnInfo
            {
                unitId = source.unitId,
                prefabType = source.prefabType,
                prefabName = source.prefabName,
                ownerAccountId = source.ownerAccountId,
                x = source.x,
                y = source.y,
                z = source.z,
                hasScale = source.hasScale,
                scaleX = source.scaleX,
                scaleY = source.scaleY,
                scaleZ = source.scaleZ,
                hasCollider = source.hasCollider,
                colliderEnabled = source.colliderEnabled,
                colliderSizeX = source.colliderSizeX,
                colliderSizeY = source.colliderSizeY,
                colliderSizeZ = source.colliderSizeZ,
                health = source.health,
                mana = source.mana,
                hasHealth = source.hasHealth,
                hasMana = source.hasMana,
                characterData = source.characterData,
                hasFaction = source.hasFaction,
                faction = source.faction,
                hasInteractableData = source.hasInteractableData,
                interactionKind = source.interactionKind,
                interactionDataId = source.interactionDataId,
                interactionAmount = source.interactionAmount,
                interactionVariant = source.interactionVariant,
                interactionRangeSq = source.interactionRangeSq,
                interactionEnabled = source.interactionEnabled,
                hasMonsterSpawnData = source.hasMonsterSpawnData,
                monsterSaveId = source.monsterSaveId,
                monsterRegionId = source.monsterRegionId,
                monsterSquadId = source.monsterSquadId,
                monsterIsBoss = source.monsterIsBoss,
                hasExitData = source.hasExitData,
                exitRegionId = source.exitRegionId,
                exitTargetThemeKey = source.exitTargetThemeKey,
                exitTargetFloor = source.exitTargetFloor,
                exitRequiresRoomClear = source.exitRequiresRoomClear,
                exitIsOpen = source.exitIsOpen,
                hasTreasureData = source.hasTreasureData,
                treasureRegionId = source.treasureRegionId,
                treasureRandomSeed = source.treasureRandomSeed,
                treasureInterestSize = source.treasureInterestSize,
                treasureIsOpened = source.treasureIsOpened,
                treasureCandidateItemIds = source.treasureCandidateItemIds == null
                    ? null
                    : (int[])source.treasureCandidateItemIds.Clone(),
            };
        }

        private static bool TryGetSpawnQueue(EntityManager entityManager, out NetworkEntitySpawnQueueComponent spawnQueue)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkEntitySpawnQueueComponent>());
            if (query.IsEmptyIgnoreFilter)
            {
                spawnQueue = null;
                return false;
            }

            Entity queueEntity = query.GetSingletonEntity();
            spawnQueue = entityManager.GetComponentObject<NetworkEntitySpawnQueueComponent>(queueEntity);
            return spawnQueue != null;
        }

        private static void ApplySpatialData(EntityManager entityManager, Entity entity, NetworkEntitySpawnInfo entityInfo)
        {
            if (entityInfo.hasScale)
            {
                PostTransformMatrix matrix = new()
                {
                    Value = float4x4.Scale(new float3(entityInfo.scaleX, entityInfo.scaleY, entityInfo.scaleZ)),
                };
                if (entityManager.HasComponent<PostTransformMatrix>(entity))
                {
                    entityManager.SetComponentData(entity, matrix);
                }
                else
                {
                    entityManager.AddComponentData(entity, matrix);
                }
            }

            if (!entityInfo.hasCollider)
            {
                return;
            }

            if (!entityInfo.colliderEnabled)
            {
                if (entityManager.HasComponent<PhysicsCollider>(entity))
                {
                    entityManager.RemoveComponent<PhysicsCollider>(entity);
                }
                return;
            }

            if (!entityManager.HasComponent<PhysicsCollider>(entity))
            {
                return;
            }

            PhysicsCollider collider = entityManager.GetComponentData<PhysicsCollider>(entity);
            collider.Value = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(
                    Mathf.Max(0.001f, entityInfo.colliderSizeX),
                    Mathf.Max(0.001f, entityInfo.colliderSizeY),
                    Mathf.Max(0.001f, entityInfo.colliderSizeZ)),
                BevelRadius = 0f,
            });
            entityManager.SetComponentData(entity, collider);
        }

        private static void SetOrAddLocalTransform(EntityManager entityManager, Entity entity, Vector3 position)
        {
            float3 value = new(position.x, position.y, position.z);
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = value;
                entityManager.SetComponentData(entity, transform);
                return;
            }

            entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(value, quaternion.identity, 1f));
        }

        private static void SetOrAddSpawnInfo(
            EntityManager entityManager,
            Entity entity,
            NetworkEntitySpawnInfo entityInfo)
        {
            NetworkEntitySpawnInfo snapshotInfo = CloneInfo(entityInfo);
            if (entityManager.HasComponent<NetworkEntitySpawnInfoComponent>(entity))
            {
                entityManager.GetComponentObject<NetworkEntitySpawnInfoComponent>(entity).entityInfo = snapshotInfo;
            }
            else
            {
                entityManager.AddComponentObject(entity, new NetworkEntitySpawnInfoComponent
                {
                    entityInfo = snapshotInfo,
                });
            }
        }

        private static void SetOrAddComponent<T>(EntityManager entityManager, Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
            {
                entityManager.SetComponentData(entity, value);
            }
            else
            {
                entityManager.AddComponentData(entity, value);
            }
        }
    }
}
