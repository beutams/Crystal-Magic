using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.MapDemo;

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
        private SaveData _currentSaveData;
        private int _currentSaveIndex;
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
            RuntimeDataComponent.Instance.Reset();
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

            if (_currentSaveData == null)
            {
                OnSaveFailed?.Invoke("Save failed: current save data is null.");
                Debug.LogError("[SaveDataComponent] Save failed: current save data is null.");
                return false;
            }

            try
            {
                EnsureSaveDataValid(_currentSaveData);

                _currentSaveData.SaveIndex = index;
                _currentSaveData.SaveTimestamp = DateTime.Now.Ticks;
                _currentSaveData.GameVersion = Application.version;

                string json = JsonUtility.ToJson(_currentSaveData, true);
                string filePath = GetSavePath(index);

                EnsureSaveFolderExists();
                System.IO.File.WriteAllText(filePath, json);
                CreateBackup(filePath);

                _currentSaveIndex = index;

                OnSaveSuccess?.Invoke(_currentSaveData);
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

                EnsureSaveDataValid(data);

                _currentSaveData = data;
                _currentSaveIndex = data.SaveIndex;

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
                            Timestamp = data.SaveTimestamp,
                            GameVersion = data.GameVersion,
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
            EnsureCurrentSaveDataValid();
            return _currentSaveData;
        }

        public GlobalData GetGlobalData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Global;
        }

        public TownData GetTownData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town;
        }

        public StashData GetStashData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town?.Stash;
        }

        public CharacterData GetCharacterData()
        {
            EnsureCurrentSaveDataValid();
            return GetActiveCharacterDataInternal();
        }

        public SaveLocationData GetLocationData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Location;
        }

        public EquipmentData GetEquipmentData()
        {
            EnsureCurrentSaveDataValid();
            return GetActiveCharacterDataInternal()?.Equipment;
        }

        public SkillCData GetSkillData()
        {
            EnsureCurrentSaveDataValid();
            return GetActiveCharacterDataInternal()?.Skills;
        }

        public BackpackData GetBackpackData()
        {
            EnsureCurrentSaveDataValid();
            return GetActiveCharacterDataInternal()?.Backpack;
        }

        public CharacterPropData GetCharacterPropData()
        {
            EnsureCurrentSaveDataValid();
            return GetActiveCharacterDataInternal()?.Props;
        }

        public TownData GetPersistentTownData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town;
        }

        public DungeonRunData GetDungeonRunData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.DungeonRun;
        }

        #endregion

        #region Dungeon Run
        public void EnsureDungeonRunExists(int dungeonThemeId = -1, int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();
            if (_currentSaveData.DungeonRun?.Character != null)
            {
                int normalizedThemeId = NormalizeDungeonThemeId(dungeonThemeId);
                int normalizedFloor = NormalizeDungeonFloor(dungeonFloor);
                if (_currentSaveData.DungeonRun.ThemeId != normalizedThemeId ||
                    _currentSaveData.DungeonRun.CurrentFloor != normalizedFloor)
                    _currentSaveData.DungeonRun.Seed = 0;

                _currentSaveData.DungeonRun.ThemeId = normalizedThemeId;
                _currentSaveData.DungeonRun.CurrentFloor = normalizedFloor;
                return;
            }

            _currentSaveData.DungeonRun = CreateDungeonRunFromPersistent(dungeonThemeId, dungeonFloor);
        }

        public void BeginDungeonRunFromPersistent(int dungeonThemeId = -1, int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();
            _currentSaveData.DungeonRun = CreateDungeonRunFromPersistent(dungeonThemeId, dungeonFloor);
            PublishAllDataChangedEvents();
        }

        public DungeonSettlementResult SettleDungeonRun(DungeonSettlementOutcome outcome)
        {
            EnsureCurrentSaveDataValid();
            EnsureDungeonRunExists(
                _currentSaveData.Location?.DungeonThemeId ?? -1,
                _currentSaveData.Location?.DungeonFloor ?? 1);

            DungeonRunData dungeonRun = _currentSaveData.DungeonRun;
            CharacterData settledCharacter = CloneCharacterData(dungeonRun.Character);
            int reachedFloor = Mathf.Max(1, dungeonRun.CurrentFloor);
            DungeonSettlementResult result;

            if (outcome == DungeonSettlementOutcome.Escaped)
            {
                int beforeTransferItemQuantity = CountTrackedItems(settledCharacter);
                RemoveNonTransferableItems(settledCharacter);
                int afterTransferItemQuantity = CountTrackedItems(settledCharacter);
                GetItemDelta(_currentSaveData.Town.Character, settledCharacter, out int gainedItemQuantity, out int lostItemQuantity);
                long returnedMoney = Math.Max(0L, dungeonRun.RunMoney);

                _currentSaveData.Town.StashMoney += returnedMoney;
                result = new DungeonSettlementResult
                {
                    Outcome = outcome,
                    ReachedFloor = reachedFloor,
                    ReturnedMoney = returnedMoney,
                    GainedItemQuantity = gainedItemQuantity,
                    LostItemQuantity = lostItemQuantity,
                    NonTransferableItemQuantity = Mathf.Max(0, beforeTransferItemQuantity - afterTransferItemQuantity),
                };
            }
            else
            {
                result = new DungeonSettlementResult
                {
                    Outcome = outcome,
                    ReachedFloor = reachedFloor,
                    LostItemQuantity = CountTrackedItems(settledCharacter),
                };
                ClearBackpackAndEquipment(settledCharacter);
            }

            _currentSaveData.Town.Character = settledCharacter;
            EnsureCharacterDataValid(_currentSaveData.Town.Character);
            _currentSaveData.DungeonRun = null;
            _currentSaveData.Location.AreaType = SaveAreaType.Town;
            _currentSaveData.Location.DungeonThemeId = GetInitialDungeonThemeId();
            _currentSaveData.Location.DungeonFloor = 1;
            PublishAllDataChangedEvents();
            return result;
        }

        public void ClearDungeonRun()
        {
            EnsureCurrentSaveDataValid();
            if (_currentSaveData.DungeonRun == null)
                return;

            _currentSaveData.DungeonRun = null;
            PublishAllDataChangedEvents();
        }

        #endregion

        #region Variables
        public void SetVariable(string key, double value)
        {
            EnsureCurrentSaveDataValid();
            _currentSaveData.Variables.Set(key, value);
        }

        public double GetVariable(string key, double defaultValue = 0d)
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Variables.Get(key, defaultValue);
        }

        public bool ContainsVariable(string key)
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Variables.Contains(key);
        }

        public bool Check(string expression)
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Variables.Check(expression);
        }

        #endregion

        #region Dungeon Theme Progress
        public int GetInitialDungeonThemeId()
        {
            return Mathf.Max(0, GetDungeonConfig()?.InitialThemeId ?? 0);
        }

        public bool IsDungeonThemeUnlocked(int dungeonThemeId)
        {
            EnsureCurrentSaveDataValid();
            int normalizedThemeId = NormalizeDungeonThemeId(dungeonThemeId);
            return GetVariable(GetDungeonThemeUnlockVariableKey(normalizedThemeId), 0d) > 0.5d;
        }

        public void UnlockDungeonTheme(int dungeonThemeId)
        {
            if (dungeonThemeId < 0)
                return;

            EnsureCurrentSaveDataValid();
            SetVariable(GetDungeonThemeUnlockVariableKey(dungeonThemeId), 1d);
        }

        public List<int> GetUnlockedDungeonThemeIds()
        {
            EnsureCurrentSaveDataValid();
            List<int> themeIds = new();
            foreach (DungeonThemeData theme in DataComponent.Instance.FindAll<DungeonThemeData>(static theme => theme != null))
            {
                if (IsDungeonThemeUnlocked(theme.Id))
                    themeIds.Add(theme.Id);
            }

            return themeIds;
        }

        #endregion

        #region Location
        public void SetCurrentLocation(SaveAreaType areaType, int dungeonFloor = 1, int dungeonThemeId = -1)
        {
            EnsureCurrentSaveDataValid();
            _currentSaveData.Location.AreaType = areaType;
            _currentSaveData.Location.DungeonThemeId = NormalizeDungeonThemeId(dungeonThemeId);
            _currentSaveData.Location.DungeonFloor = NormalizeDungeonFloor(dungeonFloor);
        }

        public LoadGameContext CreateLoadGameContext(SaveAreaType areaType, int dungeonFloor = 1, int dungeonThemeId = -1)
        {
            EnsureCurrentSaveDataValid();

            return new LoadGameContext
            {
                SaveData = _currentSaveData,
                SaveIndex = GetCurrentSaveIndex(),
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
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(SaveDataChangedEventName, _currentSaveData));
        }

        public void NotifyGlobalDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(GlobalDataChangedEventName, _currentSaveData.Global));
            NotifySaveDataChanged();
        }

        public void NotifyTownDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(TownDataChangedEventName, GetTownData()));
            NotifySaveDataChanged();
        }

        public void NotifyStashDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(StashDataChangedEventName, GetStashData()));
            NotifyTownDataChanged();
        }

        public void NotifyCharacterDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterDataChangedEventName, GetCharacterData()));
            NotifyTownDataChanged();
        }

        public void NotifyBackpackDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(BackpackDataChangedEventName, GetBackpackData()));
            NotifyCharacterDataChanged();
        }

        public void NotifyCharacterPropDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterPropDataChangedEventName, GetCharacterPropData()));
            NotifyCharacterDataChanged();
        }

        public void NotifyEquipmentDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(EquipmentDataChangedEventName, GetEquipmentData()));
            NotifyCharacterDataChanged();
        }

        public void NotifySkillDataChanged()
        {
            EnsureCurrentSaveDataValid();
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
            SaveData data = CreateNewSaveData();
            GameConfig gameConfig = GetGameConfig();
            data.Town.StashMoney = gameConfig.StartingGold;

            _currentSaveData = data;
            _currentSaveIndex = index;
            return SaveToSlot(index);
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

            if (data.Town == null)
            {
                data.Town = new TownData();
                repairedPaths?.Add("Town");
            }

            EnsureTownDataValid(data.Town, repairedPaths);

            if (data.DungeonRun != null)
            {
                EnsureDungeonRunDataValid(data.DungeonRun, data.Town.Character, data.Location.DungeonThemeId, repairedPaths);
            }
            else if (data.Location.AreaType == SaveAreaType.Dungeon)
            {
                data.DungeonRun = CreateDungeonRunFromPersistent(
                    data.Town.Character,
                    data.Location.DungeonThemeId,
                    data.Location.DungeonFloor);
                repairedPaths?.Add("DungeonRun");
            }

            EnsureDungeonThemeUnlocksInitialized(data);
            LogValidationRepairsIfNeeded(data, repairedPaths, logRepairs);
        }

        private void EnsureTownDataValid(TownData data, List<string> repairedPaths = null)
        {
            if (data == null)
                return;

            if (data.Stash == null)
            {
                data.Stash = new StashData();
                repairedPaths?.Add("Town.Stash");
            }

            if (data.Character == null)
            {
                data.Character = new CharacterData();
                repairedPaths?.Add("Town.Character");
            }

            EnsureStashDataValid(data.Stash, repairedPaths);
            EnsureCharacterDataValid(data.Character, repairedPaths, "Town.Character");
        }

        private void EnsureStashDataValid(StashData data, List<string> repairedPaths = null)
        {
            if (data == null)
                return;

            if (data.Items == null)
            {
                data.Items = new List<InventoryItemData>();
                repairedPaths?.Add("Town.Stash.Items");
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
            CharacterData fallbackCharacter = null,
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
            if (data.Character == null)
            {
                data.Character = CloneCharacterData(fallbackCharacter);
                repairedPaths?.Add("DungeonRun.Character");
            }

            EnsureCharacterDataValid(data.Character, repairedPaths, "DungeonRun.Character");

            if (data.Monsters == null)
            {
                data.Monsters = new List<MonsterStateData>();
                repairedPaths?.Add("DungeonRun.Monsters");
            }

            if (data.ItemDrops == null)
            {
                data.ItemDrops = new List<ItemDropData>();
                repairedPaths?.Add("DungeonRun.ItemDrops");
            }
        }

        private CharacterData GetActiveCharacterDataInternal()
        {
            if (_currentSaveData == null)
                return null;

            return _currentSaveData.Location?.AreaType == SaveAreaType.Dungeon && _currentSaveData.DungeonRun?.Character != null
                ? _currentSaveData.DungeonRun.Character
                : _currentSaveData.Town?.Character;
        }

        private long GetPreviewStashMoney(SaveData data)
        {
            if (data == null)
                return 0;

            return data.Town?.StashMoney ?? 0;
        }

        private DungeonRunData CreateDungeonRunFromPersistent(int dungeonThemeId, int dungeonFloor)
        {
            return CreateDungeonRunFromPersistent(GetPersistentTownData()?.Character, dungeonThemeId, dungeonFloor);
        }

        private DungeonRunData CreateDungeonRunFromPersistent(CharacterData sourceCharacter, int dungeonThemeId, int dungeonFloor)
        {
            DungeonRunData data = new DungeonRunData
            {
                RunId = Guid.NewGuid().ToString("N"),
                RunTimestamp = DateTime.Now.Ticks,
                ThemeId = NormalizeDungeonThemeId(dungeonThemeId),
                CurrentFloor = NormalizeDungeonFloor(dungeonFloor),
                Character = CloneCharacterData(sourceCharacter),
                Monsters = new List<MonsterStateData>(),
                ItemDrops = new List<ItemDropData>(),
            };
            data.BaseSeed = DeriveDungeonRunBaseSeed(data);
            EnsureDungeonRunDataValid(data, sourceCharacter, dungeonThemeId);
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

        private void ClearBackpackAndEquipment(CharacterData data)
        {
            if (data == null)
                return;

            EnsureCharacterDataValid(data);
            data.Backpack.Items.Clear();
            data.Props.ClearSlots();
            data.Equipment = new EquipmentData();
            ClearSkillChains(data);
        }

        private void RemoveNonTransferableItems(CharacterData data)
        {
            if (data == null)
                return;

            EnsureCharacterDataValid(data);

            if (data.Backpack?.Items != null)
            {
                data.Backpack.Items.RemoveAll(item => item != null && IsItemNonTransferable(item.ItemId));
            }

            if (data.Props?.Slots != null)
            {
                for (int i = 0; i < data.Props.Slots.Count; i++)
                {
                    CharacterPropSlotData slot = data.Props.Slots[i];
                    if (slot == null || slot.ItemId < 0)
                        continue;

                    if (IsItemNonTransferable(slot.ItemId))
                        slot.Clear();
                }
            }

            if (data.Equipment != null)
            {
                if (IsItemNonTransferable(data.Equipment.MagicStoneId))
                    data.Equipment.MagicStoneId = -1;

                if (data.Equipment.SpiritSlots != null)
                {
                    for (int i = 0; i < data.Equipment.SpiritSlots.Length; i++)
                    {
                        if (IsItemNonTransferable(data.Equipment.SpiritSlots[i]))
                            data.Equipment.SpiritSlots[i] = -1;
                    }
                }
            }

            if (data.Skills?.Chains != null)
            {
                for (int i = 0; i < data.Skills.Chains.Length; i++)
                {
                    SkillChainData chain = data.Skills.Chains[i];
                    if (chain?.Slots == null)
                        continue;

                    for (int slotIndex = 0; slotIndex < chain.Slots.Count; slotIndex++)
                    {
                        SkillChainSlotData slot = chain.Slots[slotIndex];
                        if (slot == null)
                            continue;

                        if (IsItemNonTransferable(slot.SkillStoneItemId))
                        {
                            slot.SkillStoneItemId = -1;
                            slot.SkillAdditionId = -1;
                        }
                    }
                }
            }
        }

        private bool IsItemNonTransferable(int itemId)
        {
            if (itemId < 0 || DataComponent.Instance == null)
                return false;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            return itemData != null && itemData.IsNonTransferable;
        }

        private static int CountTrackedItems(CharacterData data)
        {
            Dictionary<int, int> itemCounts = GetTrackedItemCounts(data);
            int total = 0;
            foreach (int quantity in itemCounts.Values)
                total += quantity;

            return total;
        }

        private static void GetItemDelta(CharacterData baseline, CharacterData result, out int gained, out int lost)
        {
            Dictionary<int, int> baselineCounts = GetTrackedItemCounts(baseline);
            Dictionary<int, int> resultCounts = GetTrackedItemCounts(result);
            HashSet<int> itemIds = new(baselineCounts.Keys);
            itemIds.UnionWith(resultCounts.Keys);

            gained = 0;
            lost = 0;
            foreach (int itemId in itemIds)
            {
                baselineCounts.TryGetValue(itemId, out int baselineQuantity);
                resultCounts.TryGetValue(itemId, out int resultQuantity);
                int delta = resultQuantity - baselineQuantity;
                if (delta > 0)
                    gained += delta;
                else
                    lost -= delta;
            }
        }

        private static Dictionary<int, int> GetTrackedItemCounts(CharacterData data)
        {
            Dictionary<int, int> counts = new();
            if (data == null)
                return counts;

            if (data.Backpack?.Items != null)
            {
                for (int i = 0; i < data.Backpack.Items.Count; i++)
                {
                    InventoryItemData item = data.Backpack.Items[i];
                    AddTrackedItemCount(counts, item?.ItemId ?? -1, item?.Quantity ?? 0);
                }
            }

            if (data.Props?.Slots != null)
            {
                for (int i = 0; i < data.Props.Slots.Count; i++)
                {
                    CharacterPropSlotData slot = data.Props.Slots[i];
                    AddTrackedItemCount(counts, slot?.ItemId ?? -1, slot?.Quantity ?? 0);
                }
            }

            if (data.Equipment != null)
            {
                AddTrackedItemCount(counts, data.Equipment.MagicStoneId, 1);
                if (data.Equipment.SpiritSlots != null)
                {
                    for (int i = 0; i < data.Equipment.SpiritSlots.Length; i++)
                        AddTrackedItemCount(counts, data.Equipment.SpiritSlots[i], 1);
                }
            }

            if (data.Skills?.Chains != null)
            {
                for (int chainIndex = 0; chainIndex < data.Skills.Chains.Length; chainIndex++)
                {
                    SkillChainData chain = data.Skills.Chains[chainIndex];
                    if (chain?.Slots == null)
                        continue;

                    for (int slotIndex = 0; slotIndex < chain.Slots.Count; slotIndex++)
                        AddTrackedItemCount(counts, chain.Slots[slotIndex]?.SkillStoneItemId ?? -1, 1);
                }
            }

            return counts;
        }

        private static void AddTrackedItemCount(Dictionary<int, int> counts, int itemId, int quantity)
        {
            if (itemId < 0 || quantity <= 0)
                return;

            counts.TryGetValue(itemId, out int currentQuantity);
            counts[itemId] = currentQuantity + quantity;
        }

        private static void ClearSkillChains(CharacterData data)
        {
            if (data?.Skills?.Chains == null)
                return;

            for (int chainIndex = 0; chainIndex < data.Skills.Chains.Length; chainIndex++)
            {
                SkillChainData chain = data.Skills.Chains[chainIndex];
                if (chain?.Slots == null)
                    continue;

                for (int slotIndex = 0; slotIndex < chain.Slots.Count; slotIndex++)
                {
                    SkillChainSlotData slot = chain.Slots[slotIndex];
                    if (slot == null)
                        continue;

                    slot.SkillStoneItemId = -1;
                    slot.SkillAdditionId = -1;
                }
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

        #region Clone And Repair Helpers
        private CharacterData CloneCharacterData(CharacterData source)
        {
            CharacterData clone = DeepClone(source);
            clone ??= new CharacterData();
            EnsureCharacterDataValid(clone);
            return clone;
        }

        private T DeepClone<T>(T source) where T : class
        {
            if (source == null)
                return null;

            string json = JsonUtility.ToJson(source);
            return JsonUtility.FromJson<T>(json);
        }

        private void EnsureCurrentSaveDataValid()
        {
            if (_currentSaveData == null)
            {
                _currentSaveData = new SaveData();
                Debug.LogWarning("[SaveDataComponent] Current save data was null. A new SaveData instance was created during validation.");
            }

            EnsureSaveDataValid(_currentSaveData);
        }

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
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(GlobalDataChangedEventName, _currentSaveData.Global));
            EventComponent.Instance.Publish(new CommonGameEvent(TownDataChangedEventName, GetTownData()));
            EventComponent.Instance.Publish(new CommonGameEvent(StashDataChangedEventName, GetStashData()));
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterDataChangedEventName, GetCharacterData()));
            EventComponent.Instance.Publish(new CommonGameEvent(BackpackDataChangedEventName, GetBackpackData()));
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterPropDataChangedEventName, GetCharacterPropData()));
            EventComponent.Instance.Publish(new CommonGameEvent(EquipmentDataChangedEventName, GetEquipmentData()));
            EventComponent.Instance.Publish(new CommonGameEvent(SkillDataChangedEventName, GetSkillData()));
            EventComponent.Instance.Publish(new CommonGameEvent(SaveDataChangedEventName, _currentSaveData));
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
            if (_currentSaveData != null)
                return _currentSaveData.SaveIndex;

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
        public long Timestamp;
        public string GameVersion;
        public long StashMoney;
        public int MaxFloor;
        public int TotalRuns;

        public DateTime GetDateTime()
        {
            return new DateTime(Timestamp);
        }

        public string GetFormattedTime()
        {
            return GetDateTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// 读档完成后的上下文信息
    /// </summary>
    public class LoadGameContext
    {
        public SaveData SaveData;
        public int SaveIndex;
        public SaveLocationData Location;

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
