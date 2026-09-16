using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using CrystalMagic.Game.Unit;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 跨 Town/Dungeon 常驻的仓库实体。货币是仓库数据的一部分。
    /// </summary>
    public sealed class StashComponent : IComponentData
    {
        public StashData Data = new();
    }

    /// <summary>
    /// 仅在 Dungeon 场景运行期间存在。当离开 Dungeon 后该实体必须被销毁。
    /// </summary>
    public sealed class DungeonRunComponent : IComponentData
    {
        public string RunId;
        public long RunTimestamp;
        public int BaseSeed;
        public int ThemeId;
        public int CurrentFloor;
        public int Seed;
        public List<UnitRuntimeData> Units = new();
        public List<ItemDropData> ItemDrops = new();
    }

    /// <summary>
    /// 当前实际主角持有的持久角色数据。Town/Dungeon 均只有这一份。
    /// </summary>
    public sealed class PlayerCharacterComponent : IComponentData
    {
        public CharacterData Data = new();
    }

    /// <summary>
    /// 存档 DTO 与 GameWorld 实体之间的转换入口。
    /// SaveDataComponent 只持有存档槽位和游戏外数据，场景内状态只从实体读取。
    /// </summary>
    public static class GameRuntimeStateUtility
    {
        public static void ImportPersistentData(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
                throw new InvalidOperationException("GameWorld must be created before importing save data.");

            DestroyEntitiesWithComponent<StashComponent>(entityManager);
            Entity stash = entityManager.CreateEntity();
            entityManager.AddComponentObject(stash, new StashComponent
            {
                Data = data.Stash ?? new StashData(),
            });
        }

        public static SaveData Export(int saveIndex, GlobalData global, SaveVariableData variables)
        {
            if (!GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
                return null;

            DungeonRunData run = GetDungeonRunData();
            SaveData data = new()
            {
                SaveIndex = saveIndex,
                Global = global ?? new GlobalData(),
                Variables = variables ?? new SaveVariableData(),
                Stash = GetStashData() ?? new StashData(),
                Character = GetPlayerCharacterData() ?? new CharacterData(),
                Location = new SaveLocationData
                {
                    AreaType = GameWorldManager.SceneMode == GameSceneMode.Dungeon
                        ? SaveAreaType.Dungeon
                        : GameWorldManager.SceneMode == GameSceneMode.Training
                            ? SaveAreaType.Training
                            : SaveAreaType.Town,
                    DungeonThemeId = run?.ThemeId ?? 0,
                    DungeonFloor = run?.CurrentFloor ?? 1,
                },
            };

            if (TryGetPlayerEntity(out _, out Entity player))
                data.Player = CreateUnitRuntimeData(entityManager, player);

            if (GameWorldManager.SceneMode != GameSceneMode.Dungeon || run == null)
                return data;

            data.DungeonRun = run;
            CaptureDungeonRuntimeState(entityManager, data.DungeonRun);
            return data;
        }

        public static StashData GetStashData()
        {
            return GameWorldManager.TryGetEntityManager(out EntityManager entityManager) &&
                   TryGetComponentObject(entityManager, out StashComponent stash)
                ? stash.Data
                : null;
        }

        public static CharacterData GetPlayerCharacterData()
        {
            return TryGetPlayerEntity(out EntityManager entityManager, out Entity player) &&
                   entityManager.HasComponent<PlayerCharacterComponent>(player)
                ? entityManager.GetComponentObject<PlayerCharacterComponent>(player)?.Data
                : null;
        }

        public static bool TryGetPlayerCharacterData(EntityManager entityManager, Entity player, out CharacterData data)
        {
            data = null;
            if (player == Entity.Null ||
                !entityManager.Exists(player) ||
                !entityManager.HasComponent<PlayerCharacterComponent>(player))
            {
                return false;
            }

            data = entityManager.GetComponentObject<PlayerCharacterComponent>(player)?.Data;
            return data != null;
        }

        public static void BindPlayerCharacterData(CharacterData data)
        {
            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                return;

            if (entityManager.HasComponent<PlayerCharacterComponent>(player))
                entityManager.GetComponentObject<PlayerCharacterComponent>(player).Data = data ?? new CharacterData();
            else
                entityManager.AddComponentObject(player, new PlayerCharacterComponent { Data = data ?? new CharacterData() });
        }

        public static void ClearPlayerCharacterData()
        {
            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player) ||
                !entityManager.HasComponent<PlayerCharacterComponent>(player))
            {
                return;
            }

            entityManager.RemoveComponent<PlayerCharacterComponent>(player);
        }

        public static DungeonRunData GetDungeonRunData()
        {
            if (!GameWorldManager.TryGetEntityManager(out EntityManager entityManager) ||
                !TryGetComponentObject(entityManager, out DungeonRunComponent run))
            {
                return null;
            }

            return new DungeonRunData
            {
                RunId = run.RunId,
                RunTimestamp = run.RunTimestamp,
                BaseSeed = run.BaseSeed,
                ThemeId = run.ThemeId,
                CurrentFloor = run.CurrentFloor,
                Seed = run.Seed,
                Units = run.Units,
                ItemDrops = run.ItemDrops,
            };
        }

        public static void CreateDungeonRun(DungeonRunData data)
        {
            if (data == null || !GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
                return;

            ClearDungeonRun();
            Entity entity = entityManager.CreateEntity();
            entityManager.AddComponentObject(entity, new DungeonRunComponent
            {
                RunId = data.RunId,
                RunTimestamp = data.RunTimestamp,
                BaseSeed = data.BaseSeed,
                ThemeId = data.ThemeId,
                CurrentFloor = data.CurrentFloor,
                Seed = data.Seed,
                Units = data.Units ?? new List<UnitRuntimeData>(),
                ItemDrops = data.ItemDrops ?? new List<ItemDropData>(),
            });
        }

        public static void UpdateDungeonRun(DungeonRunData data)
        {
            if (data == null || !GameWorldManager.TryGetEntityManager(out EntityManager entityManager) ||
                !TryGetComponentObject(entityManager, out DungeonRunComponent run))
            {
                return;
            }

            run.RunId = data.RunId;
            run.RunTimestamp = data.RunTimestamp;
            run.BaseSeed = data.BaseSeed;
            run.ThemeId = data.ThemeId;
            run.CurrentFloor = data.CurrentFloor;
            run.Seed = data.Seed;
        }

        public static DungeonRuntimeMapComponent GetDungeonRuntimeMap()
        {
            return GameWorldManager.TryGetEntityManager(out EntityManager entityManager) &&
                   TryGetComponentObject(entityManager, out DungeonRuntimeMapComponent map)
                ? map
                : null;
        }

        public static void SetDungeonRuntimeMap(
            OpenFieldDungeonLayout layout,
            RuntimeDungeonSceneData sceneData,
            int floor,
            int seed,
            int attemptCount)
        {
            if (!GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
                return;

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DungeonRunComponent>());
            if (query.IsEmptyIgnoreFilter)
                return;

            Entity dungeonRunEntity = query.GetSingletonEntity();
            DungeonRuntimeMapComponent map;
            if (entityManager.HasComponent<DungeonRuntimeMapComponent>(dungeonRunEntity))
                map = entityManager.GetComponentObject<DungeonRuntimeMapComponent>(dungeonRunEntity);
            else
            {
                map = new DungeonRuntimeMapComponent();
                entityManager.AddComponentObject(dungeonRunEntity, map);
            }

            map.Set(layout, sceneData, floor, seed, attemptCount);
        }

        public static void RestoreDungeonRuntimeState(UnitRuntimeData playerState)
        {
            if (!GameWorldManager.TryGetEntityManager(out EntityManager entityManager) ||
                !TryGetComponentObject(entityManager, out DungeonRunComponent run))
            {
                return;
            }

            ApplyPlayerRuntimeState(playerState);

            EntityQuery monsterQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DungeonMonsterSpawnComponent>());
            using Unity.Collections.NativeArray<Entity> monsters = monsterQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int index = 0; index < monsters.Length; index++)
                TryRestoreDungeonUnit(entityManager, monsters[index]);

            if (run.ItemDrops == null)
                return;

            for (int index = 0; index < run.ItemDrops.Count; index++)
            {
                ItemDropData drop = run.ItemDrops[index];
                WorldDropSpawnUtility.TrySpawnDrop(
                    entityManager,
                    drop.DropType,
                    drop.ItemId,
                    drop.Quantity,
                    new float3(drop.X, drop.Y, drop.Z));
            }

            run.ItemDrops.Clear();
        }

        public static void TryRestoreDungeonUnit(EntityManager entityManager, Entity entity)
        {
            if (!TryGetComponentObject(entityManager, out DungeonRunComponent run) ||
                !entityManager.HasComponent<DungeonMonsterSpawnComponent>(entity))
            {
                return;
            }

            int saveId = entityManager.GetComponentData<DungeonMonsterSpawnComponent>(entity).SaveId;
            if (run.Units == null || saveId < 0)
                return;

            for (int index = 0; index < run.Units.Count; index++)
            {
                if (run.Units[index] != null && run.Units[index].SaveId == saveId)
                {
                    ApplyUnitRuntimeData(entityManager, entity, run.Units[index]);
                    return;
                }
            }
        }

        public static void ApplyPlayerRuntimeState(UnitRuntimeData data)
        {
            if (TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                ApplyUnitRuntimeData(entityManager, player, data);
        }

        public static void ClearDungeonRun()
        {
            if (GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
                DestroyEntitiesWithComponent<DungeonRunComponent>(entityManager);
        }

        public static bool TryGetComponentObject<T>(EntityManager entityManager, out T component)
            where T : class, IComponentData
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                component = null;
                return false;
            }

            component = entityManager.GetComponentObject<T>(query.GetSingletonEntity());
            return component != null;
        }

        public static bool TryGetPlayerEntity(out EntityManager entityManager, out Entity player)
        {
            player = Entity.Null;
            if (!GameWorldManager.TryGetEntityManager(out entityManager))
                return false;

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitFactionComponent>());
            using Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                if (UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                {
                    player = entity;
                    return true;
                }
            }

            return false;
        }

        private static void CaptureDungeonRuntimeState(EntityManager entityManager, DungeonRunData run)
        {
            run.Units = new List<UnitRuntimeData>();
            EntityQuery unitQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitFactionComponent>());
            using Unity.Collections.NativeArray<Entity> units = unitQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int index = 0; index < units.Length; index++)
            {
                Entity entity = units[index];
                UnitRuntimeData state = CreateUnitRuntimeData(entityManager, entity);
                if (!UnitFactionUtility.IsPlayer(state.Faction))
                    run.Units.Add(state);
            }

            run.ItemDrops = new List<ItemDropData>();
            EntityQuery dropQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInteractableComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            using Unity.Collections.NativeArray<Entity> drops = dropQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int index = 0; index < drops.Length; index++)
            {
                UnitInteractableComponent interactable = entityManager.GetComponentData<UnitInteractableComponent>(drops[index]);
                if (interactable.Data.Kind != InteractionKind.Drop)
                    continue;

                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(drops[index]);
                run.ItemDrops.Add(new ItemDropData
                {
                    DropType = (DropRewardType)interactable.Data.Variant,
                    ItemId = interactable.Data.DataId,
                    Quantity = interactable.Data.Amount,
                    X = transform.Position.x,
                    Y = transform.Position.y,
                    Z = transform.Position.z,
                });
            }
        }

        private static UnitRuntimeData CreateUnitRuntimeData(EntityManager entityManager, Entity entity)
        {
            UnitRuntimeData state = new()
            {
                Faction = entityManager.GetComponentData<UnitFactionComponent>(entity).Value,
                UnitDataId = entityManager.HasComponent<UnitBehaviorTreeComponent>(entity)
                    ? entityManager.GetComponentObject<UnitBehaviorTreeComponent>(entity)?.UnitDataId ?? -1
                    : -1,
            };

            if (entityManager.HasComponent<DungeonMonsterSpawnComponent>(entity))
                state.SaveId = entityManager.GetComponentData<DungeonMonsterSpawnComponent>(entity).SaveId;

            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                state.X = transform.Position.x;
                state.Y = transform.Position.y;
                state.Z = transform.Position.z;
            }

            if (entityManager.HasComponent<UnitVitalityComponent>(entity))
                state.Health = entityManager.GetComponentData<UnitVitalityComponent>(entity).CurrentHealth;

            if (entityManager.HasComponent<UnitManaComponent>(entity))
                state.Mana = entityManager.GetComponentData<UnitManaComponent>(entity).CurrentMana;

            return state;
        }

        private static void ApplyUnitRuntimeData(EntityManager entityManager, Entity entity, UnitRuntimeData data)
        {
            if (data == null || entity == Entity.Null || !entityManager.Exists(entity))
                return;

            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = new float3(data.X, data.Y, data.Z);
                entityManager.SetComponentData(entity, transform);
                if (entityManager.HasComponent<UnitMoveComponent>(entity))
                {
                    UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
                    move.NetworkDirty = 1;
                    entityManager.SetComponentData(entity, move);
                }
            }

            if (entityManager.HasComponent<UnitVitalityComponent>(entity))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                vitality.CurrentHealth = data.Health;
                vitality.NetworkDirty = 1;
                entityManager.SetComponentData(entity, vitality);
            }

            if (entityManager.HasComponent<UnitManaComponent>(entity))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
                mana.CurrentMana = data.Mana;
                mana.NetworkDirty = 1;
                entityManager.SetComponentData(entity, mana);
            }
        }

        private static void DestroyEntitiesWithComponent<T>(EntityManager entityManager)
            where T : IComponentData
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.IsEmptyIgnoreFilter)
                entityManager.DestroyEntity(query);
        }
    }
}
