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
        public const string DungeonUnlockedStartFloorVariablePrefix = "DungeonUnlockedStartFloor_";
        public const string DungeonHighestReachedFloorVariableKey = "DungeonHighestReachedFloor";
        public const int DungeonStartFloorUnlockInterval = 20;
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
        public void EnsureDungeonRunExists(int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();
            if (_currentSaveData.DungeonRun?.Character != null)
            {
                int normalizedFloor = Mathf.Max(1, dungeonFloor);
                if (_currentSaveData.DungeonRun.CurrentFloor != normalizedFloor)
                    _currentSaveData.DungeonRun.Seed = 0;

                _currentSaveData.DungeonRun.CurrentFloor = normalizedFloor;
                return;
            }

            _currentSaveData.DungeonRun = CreateDungeonRunFromPersistent(dungeonFloor);
        }

        public void BeginDungeonRunFromPersistent(int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();
            _currentSaveData.DungeonRun = CreateDungeonRunFromPersistent(dungeonFloor);
            PublishAllDataChangedEvents();
        }

        public void CommitDungeonRunToPersistent(bool clearRun = true, bool includeRunMoney = true, bool removeNonTransferableItems = true)
        {
            EnsureCurrentSaveDataValid();
            if (_currentSaveData.DungeonRun?.Character == null)
                return;

            if (removeNonTransferableItems)
                RemoveNonTransferableItems(_currentSaveData.DungeonRun.Character);

            if (includeRunMoney && _currentSaveData.DungeonRun.RunMoney > 0)
                _currentSaveData.Town.StashMoney += _currentSaveData.DungeonRun.RunMoney;

            _currentSaveData.Town.Character = CloneCharacterData(_currentSaveData.DungeonRun.Character);
            EnsureCharacterDataValid(_currentSaveData.Town.Character);

            if (clearRun)
                _currentSaveData.DungeonRun = null;

            PublishAllDataChangedEvents();
        }

        public void ApplyDungeonDeathAndCommit()
        {
            EnsureCurrentSaveDataValid();
            EnsureDungeonRunExists(_currentSaveData.Location?.DungeonFloor ?? 1);

            ClearBackpackAndEquipment(_currentSaveData.DungeonRun.Character);
            CommitDungeonRunToPersistent(includeRunMoney: false, removeNonTransferableItems: false);
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

        #region Dungeon Progress
        public void EnsureDungeonStartFloorUnlocksInitialized()
        {
            EnsureCurrentSaveDataValid();
            EnsureDungeonStartFloorUnlocksInitialized(_currentSaveData);
        }

        public void UpdateDungeonReachedFloorProgress(int dungeonFloor)
        {
            EnsureCurrentSaveDataValid();

            int normalizedFloor = Mathf.Max(1, dungeonFloor);
            double highestReached = GetVariable(DungeonHighestReachedFloorVariableKey, 1d);
            if (normalizedFloor > highestReached)
                SetVariable(DungeonHighestReachedFloorVariableKey, normalizedFloor);

            UnlockDungeonStartFloorInternal(1);
        }

        public void UnlockDungeonStartFloorAfterBossClear(int clearedFloor)
        {
            EnsureCurrentSaveDataValid();

            int normalizedFloor = Mathf.Max(1, clearedFloor);
            if (normalizedFloor % DungeonStartFloorUnlockInterval != 0)
                return;

            UnlockDungeonStartFloorInternal(normalizedFloor + 1);
        }

        public bool IsDungeonStartFloorUnlocked(int startFloor)
        {
            EnsureCurrentSaveDataValid();

            int normalizedFloor = NormalizeDungeonStartFloor(startFloor);
            if (normalizedFloor == 1)
                return true;

            return GetVariable(GetDungeonStartFloorUnlockVariableKey(normalizedFloor), 0d) > 0.5d;
        }

        public List<int> GetUnlockedDungeonStartFloors()
        {
            EnsureCurrentSaveDataValid();
            EnsureDungeonStartFloorUnlocksInitialized();

            List<int> floors = new() { 1 };
            int highestReachedFloor = Mathf.Max(1, (int)Math.Round(GetVariable(DungeonHighestReachedFloorVariableKey, 1d)));
            int maxCandidateFloor = Mathf.Max(1, ((highestReachedFloor / DungeonStartFloorUnlockInterval) + 1) * DungeonStartFloorUnlockInterval + 1);
            for (int startFloor = DungeonStartFloorUnlockInterval + 1; startFloor <= maxCandidateFloor; startFloor += DungeonStartFloorUnlockInterval)
            {
                if (!IsDungeonStartFloorUnlocked(startFloor))
                    continue;

                floors.Add(startFloor);
            }

            return floors;
        }

        #endregion

        #region Location
        public void SetCurrentLocation(SaveAreaType areaType, int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();
            _currentSaveData.Location.AreaType = areaType;
            _currentSaveData.Location.DungeonFloor = Mathf.Max(1, dungeonFloor);
        }

        public LoadGameContext CreateLoadGameContext(SaveAreaType areaType, int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();

            return new LoadGameContext
            {
                SaveData = _currentSaveData,
                SaveIndex = GetCurrentSaveIndex(),
                Location = new SaveLocationData
                {
                    AreaType = areaType,
                    DungeonFloor = Mathf.Max(1, dungeonFloor),
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

            data.Location.DungeonFloor = Mathf.Max(1, data.Location.DungeonFloor);

            if (data.Town == null)
            {
                data.Town = new TownData();
                repairedPaths?.Add("Town");
            }

            EnsureTownDataValid(data.Town, repairedPaths);

            if (data.DungeonRun != null)
            {
                EnsureDungeonRunDataValid(data.DungeonRun, data.Town.Character, repairedPaths);
            }
            else if (data.Location.AreaType == SaveAreaType.Dungeon)
            {
                data.DungeonRun = CreateDungeonRunFromPersistent(data.Town.Character, data.Location.DungeonFloor);
                repairedPaths?.Add("DungeonRun");
            }

            EnsureDungeonStartFloorUnlocksInitialized(data);
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

        private void EnsureDungeonRunDataValid(DungeonRunData data, CharacterData fallbackCharacter = null, List<string> repairedPaths = null)
        {
            if (data == null)
                return;

            if (string.IsNullOrWhiteSpace(data.RunId))
                data.RunId = Guid.NewGuid().ToString("N");

            if (data.RunTimestamp <= 0)
                data.RunTimestamp = DateTime.Now.Ticks;

            if (data.BaseSeed == 0)
                data.BaseSeed = DeriveDungeonRunBaseSeed(data);

            data.CurrentFloor = Mathf.Max(1, data.CurrentFloor);
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

        private DungeonRunData CreateDungeonRunFromPersistent(int dungeonFloor)
        {
            return CreateDungeonRunFromPersistent(GetPersistentTownData()?.Character, dungeonFloor);
        }

        private DungeonRunData CreateDungeonRunFromPersistent(CharacterData sourceCharacter, int dungeonFloor)
        {
            DungeonRunData data = new DungeonRunData
            {
                RunId = Guid.NewGuid().ToString("N"),
                RunTimestamp = DateTime.Now.Ticks,
                CurrentFloor = Mathf.Max(1, dungeonFloor),
                Character = CloneCharacterData(sourceCharacter),
                Monsters = new List<MonsterStateData>(),
                ItemDrops = new List<ItemDropData>(),
            };
            data.BaseSeed = DeriveDungeonRunBaseSeed(data);
            EnsureDungeonRunDataValid(data, sourceCharacter);
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

                int result = (int)(hash == 0 ? (uint)DungeonMakerTunnelingGenerator.DefaultSeed : hash);
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

        #region Dungeon Unlock Helpers
        private void UnlockDungeonStartFloorInternal(int startFloor)
        {
            int normalizedFloor = NormalizeDungeonStartFloor(startFloor);
            SetVariable(GetDungeonStartFloorUnlockVariableKey(normalizedFloor), 1d);
        }

        private static void EnsureDungeonStartFloorUnlocksInitialized(SaveData data)
        {
            if (data?.Variables == null)
                return;

            string unlockKey = GetDungeonStartFloorUnlockVariableKey(1);
            if (!data.Variables.Contains(unlockKey))
                data.Variables.Set(unlockKey, 1d);

            if (!data.Variables.Contains(DungeonHighestReachedFloorVariableKey))
                data.Variables.Set(DungeonHighestReachedFloorVariableKey, 1d);
        }

        private static int NormalizeDungeonStartFloor(int startFloor)
        {
            int normalizedFloor = Mathf.Max(1, startFloor);
            if (normalizedFloor == 1)
                return 1;

            int remainder = (normalizedFloor - 1) % DungeonStartFloorUnlockInterval;
            return remainder == 0 ? normalizedFloor : normalizedFloor - remainder + DungeonStartFloorUnlockInterval;
        }

        public static string GetDungeonStartFloorUnlockVariableKey(int startFloor)
        {
            return $"{DungeonUnlockedStartFloorVariablePrefix}{NormalizeDungeonStartFloor(startFloor)}";
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
