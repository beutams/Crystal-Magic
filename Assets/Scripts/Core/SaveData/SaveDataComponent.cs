using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core {
    /// <summary>
    /// 存档系统组件
    /// </summary>
    public class SaveDataComponent : GameComponent<SaveDataComponent>
    {
        #region Event Names
        public const string SaveDataChangedEventName = "SaveData.Changed";
        public const string GlobalDataChangedEventName = "SaveData.Global.Changed";
        public const string TownDataChangedEventName = "SaveData.Town.Changed";
        public const string StashDataChangedEventName = "SaveData.Town.Stash.Changed";
        public const string CharacterDataChangedEventName = "SaveData.Town.Character.Changed";
        public const string BackpackDataChangedEventName = "SaveData.Town.Character.Backpack.Changed";
        public const string CharacterPropDataChangedEventName = "SaveData.Town.Character.Props.Changed";
        public const string EquipmentDataChangedEventName = "SaveData.Town.Character.Equipment.Changed";
        public const string SkillDataChangedEventName = "SaveData.Town.Character.Skill.Changed";
        #endregion

        #region Component
        public override int Priority => 18;
        #endregion

        #region Constants
        private const string SAVE_FOLDER = "SaveData";
        public const string DungeonThemeUnlockedVariablePrefix = "DungeonThemeUnlocked_";
        #endregion

        #region Fields
        private int _currentSaveIndex;
        private string _currentSaveGuid;
        private GlobalData _globalData;
        private SaveVariableData _variables;

        public int CurrentSaveIndex => _currentSaveIndex;
        public string CurrentSaveGuid => _currentSaveGuid;
        #endregion

        #region Events
        public event Action<SaveData> OnSaveSuccess;
        public event Action<string> OnSaveFailed;
        public event Action<SaveData> OnLoadSuccess;
        public event Action<string> OnLoadFailed;
        #endregion

        #region Lifecycle
        public override void Initialize()
        {
            base.Initialize();
            EnsureSaveFolderExists();
            _currentSaveIndex = -1;
            _currentSaveGuid = null;
            _globalData = new GlobalData();
            _variables = new SaveVariableData();
            Debug.Log("[SaveDataComponent] Initialized");
        }

        public override void Cleanup()
        {
            base.Cleanup();
        }
        #endregion

        /// <summary>
        /// 保存当前存档。若当前没有已选槽位，则保存到默认槽位 0。
        /// </summary>
        #region Save Load
        public bool Save()
        {
            int currentSaveIndex = GetCurrentSaveIndex();
            if (currentSaveIndex < 0)
            {
                OnSaveFailed?.Invoke("Save failed: no save slot selected.");
                Debug.LogError("[SaveDataComponent] Save failed: no save slot selected.");
                return false;
            }

            return SaveToSlot(currentSaveIndex);
        }

        /// <summary>
        /// 保存到指定槽位编号。
        /// </summary>
        public bool SaveToSlot(int index)
        {
            if (index < 0)
            {
                OnSaveFailed?.Invoke($"Save failed: invalid slot index {index}.");
                Debug.LogError($"[SaveDataComponent] Save failed: invalid slot index {index}.");
                return false;
            }

            SaveData data = GameRuntimeStateUtility.Export(index, _globalData, _variables);
            if (data == null)
            {
                OnSaveFailed?.Invoke("Save failed: GameWorld runtime state is unavailable.");
                Debug.LogError("[SaveDataComponent] Save failed: GameWorld runtime state is unavailable.");
                return false;
            }

            try
            {
                data.SaveIndex = index;
                data.SaveGuid = index == _currentSaveIndex && Guid.TryParse(_currentSaveGuid, out Guid currentSaveGuid)
                    ? currentSaveGuid.ToString("N")
                    : Guid.NewGuid().ToString("N");
                EnsureSaveDataValid(data);

                string json = JsonUtility.ToJson(data, true);
                string filePath = GetSavePath(index);

                EnsureSaveFolderExists();
                System.IO.File.WriteAllText(filePath, json);
                CreateBackup(filePath);

                _currentSaveIndex = index;
                _currentSaveGuid = data.SaveGuid;

                OnSaveSuccess?.Invoke(data);
                Debug.Log($"[SaveDataComponent] Game saved to slot index: {index}");
                return true;
            }
            catch (Exception ex)
            {
                OnSaveFailed?.Invoke($"Save failed: {ex.Message}");
                Debug.LogError($"[SaveDataComponent] Error saving game: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 读取当前存档。若当前没有已选槽位，则读取默认槽位 0。
        /// </summary>
        public bool Load()
        {
            int currentSaveIndex = GetCurrentSaveIndex();
            if (currentSaveIndex < 0)
            {
                OnLoadFailed?.Invoke("Load failed: no save slot selected.");
                Debug.LogError("[SaveDataComponent] Load failed: no save slot selected.");
                return false;
            }

            return LoadFromSlot(currentSaveIndex);
        }

        /// <summary>
        /// 从指定槽位编号读取。
        /// </summary>
        public bool LoadFromSlot(int index)
        {
            return LoadFromSlot(index, out _);
        }

        public bool LoadFromSlot(int index, out LoadGameContext context)
        {
            return LoadFromSlot(index, null, out context);
        }

        public bool LoadFromSlot(int index, string expectedSaveGuid, out LoadGameContext context)
        {
            context = null;
            if (index < 0)
            {
                OnLoadFailed?.Invoke($"Load failed: invalid slot index {index}.");
                Debug.LogError($"[SaveDataComponent] Load failed: invalid slot index {index}.");
                return false;
            }

            try
            {
                string filePath = GetSavePath(index);

                if (!System.IO.File.Exists(filePath))
                {
                    OnLoadFailed?.Invoke($"Save file not found: {index}");
                    return false;
                }

                string json = System.IO.File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    OnLoadFailed?.Invoke($"Failed to parse save file: {index}");
                    return false;
                }

                string storedSaveGuid = data.SaveGuid;
                EnsureSaveDataValid(data);
                if (!string.Equals(storedSaveGuid, data.SaveGuid, StringComparison.Ordinal))
                {
                    System.IO.File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
                    CreateBackup(filePath);
                }

                if (!string.IsNullOrWhiteSpace(expectedSaveGuid) &&
                    (!Guid.TryParse(expectedSaveGuid, out Guid expectedGuid) ||
                     !string.Equals(data.SaveGuid, expectedGuid.ToString("N"), StringComparison.Ordinal)))
                {
                    OnLoadFailed?.Invoke($"Load failed: save identity mismatch for slot {index}.");
                    Debug.LogError($"[SaveDataComponent] Save identity mismatch for slot index: {index}");
                    return false;
                }

                _currentSaveIndex = data.SaveIndex;
                _currentSaveGuid = data.SaveGuid;
                _globalData = data.Global;
                _variables = data.Variables;
                GameRuntimeStateUtility.ImportPersistentData(data);
                context = new LoadGameContext
                {
                    SaveIndex = _currentSaveIndex,
                    SaveGuid = _currentSaveGuid,
                    Location = data.Location,
                    Character = data.Character,
                    Player = data.Player,
                    DungeonRun = data.DungeonRun,
                };

                OnLoadSuccess?.Invoke(data);
                PublishAllDataChangedEvents();
                Debug.Log($"[SaveDataComponent] Game loaded from slot index: {index}");
                return true;
            }
            catch (Exception ex)
            {
                OnLoadFailed?.Invoke($"Load failed: {ex.Message}");
                Debug.LogError($"[SaveDataComponent] Error loading game: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有存档记录，按最新时间排序，最多返回 20 条。
        /// </summary>
        #endregion

        #region Save Records
        public List<SaveRecord> GetAllSaveRecords()
        {
            List<SaveRecord> records = new();

            try
            {
                string folderPath = GetSaveFolderPath();
                if (!System.IO.Directory.Exists(folderPath))
                    return records;

                string[] files = System.IO.Directory.GetFiles(folderPath, "*.json");
                Array.Sort(files, (a, b) =>
                    System.IO.File.GetLastWriteTime(b).CompareTo(System.IO.File.GetLastWriteTime(a)));

                int count = 0;
                foreach (string file in files)
                {
                    if (file.EndsWith(".backup.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!TryGetManualSaveSlotIndex(file, out int slotIndex))
                        continue;

                    if (count >= GetMaxSaveSlots())
                        break;

                    try
                    {
                        string json = System.IO.File.ReadAllText(file);
                        SaveData data = JsonUtility.FromJson<SaveData>(json);
                        if (data == null)
                            continue;

                        records.Add(new SaveRecord
                        {
                            SaveIndex = slotIndex,
                            StashMoney = GetPreviewStashMoney(data),
                        });
                        count++;
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDataComponent] Error getting save records: {ex.Message}");
            }

            return records;
        }

        /// <summary>
        /// 删除指定槽位编号的存档。
        /// </summary>
        private bool TryGetManualSaveSlotIndex(string filePath, out int slotIndex)
        {
            slotIndex = -1;

            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return int.TryParse(fileName, out slotIndex);
        }

        public bool DeleteSlot(int index)
        {
            try
            {
                string filePath = GetSavePath(index);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    Debug.Log($"[SaveDataComponent] Save deleted: {index}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDataComponent] Error deleting save: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Data Access
        public SaveData GetCurrentSaveData()
        {
            SaveData data = GameRuntimeStateUtility.Export(_currentSaveIndex, _globalData, _variables);
            if (data != null)
                data.SaveGuid = _currentSaveGuid;
            return data;
        }

        public GlobalData GetGlobalData()
        {
            return _globalData;
        }

        public StashData GetStashData()
        {
            return GameRuntimeStateUtility.GetStashData();
        }

        public long GetStashMoney()
        {
            return GetStashData()?.Money ?? 0;
        }

        public void AddStashMoney(long amount)
        {
            StashData stash = GetStashData();
            if (amount == 0 || stash == null)
                return;

            stash.Money = Math.Max(0L, stash.Money + amount);
            NotifyStashDataChanged();
        }

        public void AddCharacterMoney(long amount)
        {
            CharacterData character = GetCharacterData();
            if (amount == 0 || character == null)
                return;

            character.Money = Math.Max(0L, character.Money + amount);
            NotifyCharacterDataChanged();
        }

        public CharacterData GetCharacterData()
        {
            return GetActiveCharacterDataInternal();
        }

        public SaveLocationData GetLocationData()
        {
            DungeonRunData run = GetDungeonRunData();
            return new SaveLocationData
            {
                AreaType = GameWorldManager.SceneMode == GameSceneMode.Dungeon
                    ? SaveAreaType.Dungeon
                    : GameWorldManager.SceneMode == GameSceneMode.Training
                        ? SaveAreaType.Training
                        : SaveAreaType.Town,
                DungeonThemeId = run?.ThemeId ?? GetInitialDungeonThemeId(),
                DungeonFloor = run?.CurrentFloor ?? 1,
            };
        }

        public EquipmentData GetEquipmentData()
        {
            return GetActiveCharacterDataInternal()?.Equipment;
        }

        public SkillCData GetSkillData()
        {
            return GetActiveCharacterDataInternal()?.Skills;
        }

        public BackpackData GetBackpackData()
        {
            return GetActiveCharacterDataInternal()?.Backpack;
        }

        public CharacterPropData GetCharacterPropData()
        {
            return GetActiveCharacterDataInternal()?.Props;
        }

        public DungeonRunData GetDungeonRunData()
        {
            return GameRuntimeStateUtility.GetDungeonRunData();
        }

        #endregion

        #region Dungeon Run
        public void EnsureDungeonRunExists(int dungeonThemeId = -1, int dungeonFloor = 1)
        {
            if (!GameWorldManager.TryGetEntityManager(out Unity.Entities.EntityManager entityManager))
                return;

            if (GameRuntimeStateUtility.TryGetComponentObject(entityManager, out DungeonRunComponent dungeonRun))
            {
                int normalizedThemeId = NormalizeDungeonThemeId(dungeonThemeId);
                int normalizedFloor = NormalizeDungeonFloor(dungeonFloor);
                if (dungeonRun.ThemeId != normalizedThemeId || dungeonRun.CurrentFloor != normalizedFloor)
                    dungeonRun.Seed = 0;

                dungeonRun.ThemeId = normalizedThemeId;
                dungeonRun.CurrentFloor = normalizedFloor;
                return;
            }

            BeginDungeonRunFromPersistent(dungeonThemeId, dungeonFloor);
        }

        public void BeginDungeonRunFromPersistent(int dungeonThemeId = -1, int dungeonFloor = 1)
        {
            if (!GameWorldManager.TryGetEntityManager(out Unity.Entities.EntityManager entityManager))
                return;

            DungeonRunData data = CreateDungeonRunFromPersistent(dungeonThemeId, dungeonFloor);
            GameRuntimeStateUtility.CreateDungeonRun(data);
            PublishAllDataChangedEvents();
        }

        public DungeonSettlementResult SettleDungeonRun(DungeonSettlementOutcome outcome)
        {
            DungeonRunData dungeonRun = GetDungeonRunData();
            int reachedFloor = Mathf.Max(1, dungeonRun?.CurrentFloor ?? 1);
            if (outcome == DungeonSettlementOutcome.Defeated)
                GameRuntimeStateUtility.ClearPlayerCharacterData();

            GameRuntimeStateUtility.ClearDungeonRun();
            PublishAllDataChangedEvents();
            return new DungeonSettlementResult
            {
                Outcome = outcome,
                ReachedFloor = reachedFloor,
            };
        }

        public void ClearDungeonRun()
        {
            GameRuntimeStateUtility.ClearDungeonRun();
            PublishAllDataChangedEvents();
        }

        #endregion

        #region Variables
        public void SetVariable(string key, double value)
        {
            _variables?.Set(key, value);
        }

        public double GetVariable(string key, double defaultValue = 0d)
        {
            return _variables?.Get(key, defaultValue) ?? defaultValue;
        }

        public bool ContainsVariable(string key)
        {
            return _variables != null && _variables.Contains(key);
        }

        public bool Check(string expression)
        {
            return _variables != null && _variables.Check(expression);
        }

        #endregion

        #region Dungeon Theme Progress
        public int GetInitialDungeonThemeId()
        {
            return Mathf.Max(0, GetDungeonConfig()?.InitialThemeId ?? 0);
        }

        public bool IsDungeonThemeUnlocked(int dungeonThemeId)
        {
            int normalizedThemeId = NormalizeDungeonThemeId(dungeonThemeId);
            return GetVariable(GetDungeonThemeUnlockVariableKey(normalizedThemeId), 0d) > 0.5d;
        }

        public void UnlockDungeonTheme(int dungeonThemeId)
        {
            if (dungeonThemeId < 0)
                return;

            SetVariable(GetDungeonThemeUnlockVariableKey(dungeonThemeId), 1d);
        }

        public List<int> GetUnlockedDungeonThemeIds()
        {
            List<int> themeIds = new();
            foreach (DungeonThemeData theme in DataComponent.Instance.FindAll<DungeonThemeData>(static theme => theme != null))
            {
                if (IsDungeonThemeUnlocked(theme.Id))
                    themeIds.Add(theme.Id);
            }

            return themeIds;
        }

        #endregion

        #region Load Context
        public LoadGameContext CreateLoadGameContext(SaveAreaType areaType, int dungeonFloor = 1, int dungeonThemeId = -1)
        {
            return new LoadGameContext
            {
                SaveIndex = GetCurrentSaveIndex(),
                SaveGuid = _currentSaveGuid,
                Character = GetCharacterData(),
                DungeonRun = areaType == SaveAreaType.Dungeon ? GetDungeonRunData() : null,
                Location = new SaveLocationData
                {
                    AreaType = areaType,
                    DungeonThemeId = NormalizeDungeonThemeId(dungeonThemeId),
                    DungeonFloor = NormalizeDungeonFloor(dungeonFloor),
                },
            };
        }

        #endregion

        #region Change Notifications
        public void NotifySaveDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(SaveDataChangedEventName, GetCurrentSaveData()));
        }

        public void NotifyGlobalDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(GlobalDataChangedEventName, GetGlobalData()));
            NotifySaveDataChanged();
        }

        public void NotifyTownDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(TownDataChangedEventName, GetCurrentSaveData()));
            NotifySaveDataChanged();
        }

        public void NotifyStashDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(StashDataChangedEventName, GetStashData()));
            NotifyTownDataChanged();
        }

        public void NotifyCharacterDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterDataChangedEventName, GetCharacterData()));
            NotifyTownDataChanged();
        }

        public void NotifyBackpackDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(BackpackDataChangedEventName, GetBackpackData()));
            NotifyCharacterDataChanged();
        }

        public void NotifyCharacterPropDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterPropDataChangedEventName, GetCharacterPropData()));
            NotifyCharacterDataChanged();
        }

        public void NotifyEquipmentDataChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(EquipmentDataChangedEventName, GetEquipmentData()));
            NotifyCharacterDataChanged();
        }

        public void NotifySkillDataChanged()
        {
            if (GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                PlayerSkillChainUtility.Rebuild(entityManager, player);

            EventComponent.Instance.Publish(new CommonGameEvent(SkillDataChangedEventName, GetSkillData()));
            NotifyCharacterDataChanged();
        }

        #endregion

        #region Save Data Creation
        public SaveData CreateNewSaveData()
        {
            SaveData data = new SaveData();
            EnsureSaveDataValid(data, false);
            return data;
        }

        public bool CreateNewGameToSlot(int index)
        {
            if (index < 0)
                return false;

            SaveData data = CreateNewSaveData();
            GameConfig gameConfig = GetGameConfig();
            data.Stash.Money = gameConfig.StartingGold;
            data.SaveIndex = index;

            try
            {
                EnsureSaveFolderExists();
                string filePath = GetSavePath(index);
                System.IO.File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
                CreateBackup(filePath);
                _currentSaveIndex = index;
                _currentSaveGuid = data.SaveGuid;
                OnSaveSuccess?.Invoke(data);
                return true;
            }
            catch (Exception ex)
            {
                OnSaveFailed?.Invoke($"Save failed: {ex.Message}");
                Debug.LogError($"[SaveDataComponent] Error creating new game: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Paths And Files
        private string GetSaveFolderPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, SAVE_FOLDER);
        }

        private string GetSavePath(int index)
        {
            return System.IO.Path.Combine(
                GetSaveFolderPath(),
                $"{index}.json");
        }

        private void EnsureSaveFolderExists()
        {
            string folderPath = GetSaveFolderPath();
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
                Debug.Log($"[SaveDataComponent] Created save folder: {folderPath}");
            }
        }

        #endregion

        #region Validation
        private void EnsureSaveDataValid(SaveData data, bool logRepairs = true)
        {
            List<string> repairedPaths = logRepairs ? new List<string>() : null;

            if (!Guid.TryParse(data.SaveGuid, out Guid saveGuid))
            {
                data.SaveGuid = Guid.NewGuid().ToString("N");
                repairedPaths?.Add("SaveGuid");
            }
            else
            {
                string normalizedSaveGuid = saveGuid.ToString("N");
                if (!string.Equals(data.SaveGuid, normalizedSaveGuid, StringComparison.Ordinal))
                {
                    data.SaveGuid = normalizedSaveGuid;
                    repairedPaths?.Add("SaveGuid");
                }
            }

            if (data.Global == null)
            {
                data.Global = new GlobalData();
                repairedPaths?.Add("Global");
            }

            if (data.Variables == null)
            {
                data.Variables = new SaveVariableData();
                repairedPaths?.Add("Variables");
            }

            if (data.Location == null)
            {
                data.Location = new SaveLocationData();
                repairedPaths?.Add("Location");
            }

            bool isLegacyDungeonLocation = data.Location.DungeonThemeId < 0;
            data.Location.DungeonThemeId = NormalizeDungeonThemeId(data.Location.DungeonThemeId);
            data.Location.DungeonFloor = isLegacyDungeonLocation
                ? 1
                : NormalizeDungeonFloor(data.Location.DungeonFloor);

            if (data.Stash == null)
            {
                data.Stash = new StashData();
                repairedPaths?.Add("Stash");
            }

            if (data.Character == null)
            {
                data.Character = new CharacterData();
                repairedPaths?.Add("Character");
            }

            EnsureStashDataValid(data.Stash, repairedPaths);
            EnsureCharacterDataValid(data.Character, repairedPaths, "Character");

            if (data.DungeonRun != null)
            {
                EnsureDungeonRunDataValid(data.DungeonRun, data.Location.DungeonThemeId, repairedPaths);
            }
            else if (data.Location.AreaType == SaveAreaType.Dungeon)
            {
                data.DungeonRun = CreateDungeonRunFromPersistent(data.Location.DungeonThemeId, data.Location.DungeonFloor);
                repairedPaths?.Add("DungeonRun");
            }

            EnsureDungeonThemeUnlocksInitialized(data);
            LogValidationRepairsIfNeeded(data, repairedPaths, logRepairs);
        }

        private void EnsureStashDataValid(StashData data, List<string> repairedPaths = null)
        {
            if (data == null)
                return;

            if (data.Items == null)
            {
                data.Items = new List<InventoryItemData>();
                repairedPaths?.Add("Stash.Items");
            }

            if (data.Capacity <= 0)
                data.Capacity = GetGameConfig().InitialStashSize;
        }

        private void EnsureCharacterDataValid(CharacterData data, List<string> repairedPaths = null, string basePath = "Town.Character")
        {
            if (data == null)
                return;

            if (data.Equipment == null)
            {
                data.Equipment = new EquipmentData();
                repairedPaths?.Add($"{basePath}.Equipment");
            }

            if (EquipmentUtility.EnsureValid(data.Equipment))
                repairedPaths?.Add($"{basePath}.Equipment.SpiritSlots");
            EquipmentUtility.RebuildProperties(data.Equipment);

            if (data.Skills == null)
            {
                data.Skills = new SkillCData();
                repairedPaths?.Add($"{basePath}.Skills");
            }

            data.Skills.EnsureValid(repairedPaths, $"{basePath}.Skills");

            if (data.Backpack == null)
            {
                data.Backpack = new BackpackData();
                repairedPaths?.Add($"{basePath}.Backpack");
            }

            if (data.Backpack.Items == null)
            {
                data.Backpack.Items = new List<InventoryItemData>();
                repairedPaths?.Add($"{basePath}.Backpack.Items");
            }

            if (data.Backpack.Capacity <= 0)
                data.Backpack.Capacity = Mathf.Max(1, GetGameConfig().InitialBackpackSize);

            if (data.Props == null)
            {
                data.Props = new CharacterPropData();
                repairedPaths?.Add($"{basePath}.Props");
            }

            data.Props.EnsureValid(GetPropSlotCount(), GetPropShortcutSlotCount(), repairedPaths, $"{basePath}.Props");
        }

        private void EnsureDungeonRunDataValid(
            DungeonRunData data,
            int fallbackThemeId = -1,
            List<string> repairedPaths = null)
        {
            if (data == null)
                return;

            if (string.IsNullOrWhiteSpace(data.RunId))
                data.RunId = Guid.NewGuid().ToString("N");

            if (data.RunTimestamp <= 0)
                data.RunTimestamp = DateTime.Now.Ticks;

            if (data.BaseSeed == 0)
                data.BaseSeed = DeriveDungeonRunBaseSeed(data);

            bool isLegacyDungeonRun = data.ThemeId < 0;
            data.ThemeId = NormalizeDungeonThemeId(data.ThemeId < 0 ? fallbackThemeId : data.ThemeId);
            data.CurrentFloor = isLegacyDungeonRun
                ? 1
                : NormalizeDungeonFloor(data.CurrentFloor);
            if (data.Units == null)
            {
                data.Units = new List<UnitRuntimeData>();
                repairedPaths?.Add("DungeonRun.Units");
            }

            if (data.ItemDrops == null)
            {
                data.ItemDrops = new List<ItemDropData>();
                repairedPaths?.Add("DungeonRun.ItemDrops");
            }
        }

        private CharacterData GetActiveCharacterDataInternal()
        {
            return GameRuntimeStateUtility.GetPlayerCharacterData();
        }

        private long GetPreviewStashMoney(SaveData data)
        {
            if (data == null)
                return 0;

            return data.Stash?.Money ?? 0;
        }

        private DungeonRunData CreateDungeonRunFromPersistent(int dungeonThemeId, int dungeonFloor)
        {
            DungeonRunData data = new DungeonRunData
            {
                RunId = Guid.NewGuid().ToString("N"),
                RunTimestamp = DateTime.Now.Ticks,
                ThemeId = NormalizeDungeonThemeId(dungeonThemeId),
                CurrentFloor = NormalizeDungeonFloor(dungeonFloor),
                Units = new List<UnitRuntimeData>(),
                ItemDrops = new List<ItemDropData>(),
            };
            data.BaseSeed = DeriveDungeonRunBaseSeed(data);
            EnsureDungeonRunDataValid(data, dungeonThemeId);
            return data;
        }

        private static int DeriveDungeonRunBaseSeed(DungeonRunData data)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string runId = data?.RunId ?? string.Empty;
                for (int i = 0; i < runId.Length; i++)
                {
                    hash ^= runId[i];
                    hash *= 16777619u;
                }

                long timestamp = data?.RunTimestamp ?? 0L;
                hash ^= (uint)timestamp;
                hash *= 16777619u;
                hash ^= (uint)(timestamp >> 32);
                hash *= 16777619u;

                int result = (int)(hash == 0 ? 19088743u : hash);
                return result == 0 ? 1 : result;
            }
        }

        #endregion

        #region Dungeon Theme Helpers
        private void EnsureDungeonThemeUnlocksInitialized(SaveData data)
        {
            if (data?.Variables == null)
                return;

            string unlockKey = GetDungeonThemeUnlockVariableKey(GetInitialDungeonThemeId());
            if (!data.Variables.Contains(unlockKey))
                data.Variables.Set(unlockKey, 1d);
        }

        private int NormalizeDungeonThemeId(int dungeonThemeId)
        {
            return dungeonThemeId < 0 ? GetInitialDungeonThemeId() : dungeonThemeId;
        }

        private static int NormalizeDungeonFloor(int dungeonFloor)
        {
            return Mathf.Clamp(dungeonFloor, 1, DungeonConfig.LevelsPerTheme);
        }

        public static string GetDungeonThemeUnlockVariableKey(int dungeonThemeId)
        {
            return $"{DungeonThemeUnlockedVariablePrefix}{Mathf.Max(0, dungeonThemeId)}";
        }

        #endregion

        #region Repair Helpers
        private static void LogValidationRepairsIfNeeded(SaveData data, List<string> repairedPaths, bool logRepairs)
        {
            if (!logRepairs || repairedPaths == null || repairedPaths.Count == 0)
                return;

            string joinedPaths = string.Join(", ", repairedPaths.Distinct());
            Debug.LogWarning($"[SaveDataComponent] Save data validation repaired missing data by creating new instances: {joinedPaths}. SaveIndex={data?.SaveIndex}");
        }

        #endregion

        #region Publish Helpers
        private void PublishAllDataChangedEvents()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(GlobalDataChangedEventName, GetGlobalData()));
            EventComponent.Instance.Publish(new CommonGameEvent(TownDataChangedEventName, GetCurrentSaveData()));
            EventComponent.Instance.Publish(new CommonGameEvent(StashDataChangedEventName, GetStashData()));
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterDataChangedEventName, GetCharacterData()));
            EventComponent.Instance.Publish(new CommonGameEvent(BackpackDataChangedEventName, GetBackpackData()));
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterPropDataChangedEventName, GetCharacterPropData()));
            EventComponent.Instance.Publish(new CommonGameEvent(EquipmentDataChangedEventName, GetEquipmentData()));
            EventComponent.Instance.Publish(new CommonGameEvent(SkillDataChangedEventName, GetSkillData()));
            EventComponent.Instance.Publish(new CommonGameEvent(SaveDataChangedEventName, GetCurrentSaveData()));
        }

        #endregion

        #region Logging
        private void CreateBackup(string savePath)
        {
            try
            {
                string backupPath = savePath.Replace(".json", ".backup.json");
                if (System.IO.File.Exists(savePath))
                {
                    System.IO.File.Copy(savePath, backupPath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDataComponent] Failed to create backup: {ex.Message}");
            }
        }

        #endregion

        #region Config Helpers
        private int GetCurrentSaveIndex()
        {
            return _currentSaveIndex;
        }

        private GameConfig GetGameConfig()
        {
            return ConfigComponent.Instance.Get<GameConfig>();
        }

        private DungeonConfig GetDungeonConfig()
        {
            return ConfigComponent.Instance.Get<DungeonConfig>();
        }

        private int GetMaxSaveSlots()
        {
            return Mathf.Max(1, GetGameConfig().MaxSaveSlots);
        }

        private int GetPropSlotCount()
        {
            return Mathf.Max(0, GetGameConfig().BattlePropSlotCount);
        }

        private int GetPropShortcutSlotCount()
        {
            return Mathf.Max(0, GetGameConfig().BattlePropShortcutSlotCount);
        }
        #endregion
    }

    /// <summary>
    /// 存档记录信息
    /// </summary>
    [System.Serializable]
    public class SaveRecord
    {
        public int SaveIndex;
        public long StashMoney;
        public int MaxFloor;
        public int TotalRuns;
    }

    /// <summary>
    /// 读档完成后的上下文信息
    /// </summary>
    public class LoadGameContext
    {
        public int SaveIndex;
        public string SaveGuid;
        public SaveLocationData Location;
        public CharacterData Character;
        public UnitRuntimeData Player;
        public DungeonRunData DungeonRun;

        public SaveAreaType AreaType => Location?.AreaType ?? SaveAreaType.Town;
        public int DungeonThemeId => Location?.DungeonThemeId ?? -1;
        public int DungeonFloor => Mathf.Max(1, Location?.DungeonFloor ?? 1);

        public bool ShouldEnterDungeon()
        {
            return AreaType == SaveAreaType.Dungeon;
        }

        public bool ShouldEnterTraining()
        {
            return AreaType == SaveAreaType.Training;
        }

        public bool ShouldEnterTown()
        {
            return AreaType == SaveAreaType.Town;
        }
    }
}
