using System;
using System.Collections.Generic;
using UnityEngine;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core {
    /// <summary>
    /// 存档系统组件
    /// </summary>
    public class SaveDataComponent : GameComponent<SaveDataComponent>
    {
        public const string SaveDataChangedEventName = "SaveData.Changed";
        public const string GlobalDataChangedEventName = "SaveData.Global.Changed";
        public const string TownDataChangedEventName = "SaveData.Town.Changed";
        public const string StashDataChangedEventName = "SaveData.Town.Stash.Changed";
        public const string CharacterDataChangedEventName = "SaveData.Town.Character.Changed";
        public const string BackpackDataChangedEventName = "SaveData.Town.Character.Backpack.Changed";
        public const string CharacterPropDataChangedEventName = "SaveData.Town.Character.Props.Changed";
        public const string EquipmentDataChangedEventName = "SaveData.Town.Character.Equipment.Changed";
        public const string SkillDataChangedEventName = "SaveData.Town.Character.Skill.Changed";

        public override int Priority => 18;

        private const string SAVE_FOLDER = "SaveData";
        private const int CURRENT_SAVE_VERSION = 1;
        private const int CURRENT_CONTENT_VERSION = 1;
        private const int DEFAULT_SAVE_INDEX = 0;
        public const string DungeonUnlockedStartFloorVariablePrefix = "DungeonUnlockedStartFloor_";
        public const string DungeonHighestReachedFloorVariableKey = "DungeonHighestReachedFloor";
        public const int DungeonStartFloorUnlockInterval = 20;

        private SaveData _currentSaveData;
        private int _currentSaveIndex;

        public event Action<SaveData> OnSaveSuccess;
        public event Action<string> OnSaveFailed;
        public event Action<SaveData> OnLoadSuccess;
        public event Action<string> OnLoadFailed;

        public override void Initialize()
        {
            base.Initialize();
            EnsureSaveFolderExists();
            _currentSaveIndex = DEFAULT_SAVE_INDEX;
            RuntimeDataComponent.Instance.Reset();
            Debug.Log("[SaveDataComponent] Initialized");
        }

        public override void Cleanup()
        {
            base.Cleanup();
        }

        /// <summary>
        /// 保存当前存档。若当前没有已选槽位，则保存到默认槽位 0。
        /// </summary>
        public bool Save()
        {
            if (_currentSaveData == null)
            {
                _currentSaveData = new SaveData();
            }

            return SaveToSlot(GetCurrentSaveIndex());
        }

        /// <summary>
        /// 保存到指定槽位编号。
        /// </summary>
        public bool SaveToSlot(int index)
        {
            if (_currentSaveData == null)
            {
                _currentSaveData = new SaveData();
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
            return LoadFromSlot(GetCurrentSaveIndex());
        }

        /// <summary>
        /// 从指定槽位编号读取。
        /// </summary>
        public bool LoadFromSlot(int index)
        {
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

            int versionMarkerIndex = fileName.IndexOf("_v", StringComparison.OrdinalIgnoreCase);
            if (versionMarkerIndex <= 0)
                return false;

            string slotIndexText = fileName.Substring(0, versionMarkerIndex);
            return int.TryParse(slotIndexText, out slotIndex);
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

        public void EnsureDungeonRunExists(int dungeonFloor = 1)
        {
            EnsureCurrentSaveDataValid();
            if (_currentSaveData.DungeonRun?.Character != null)
            {
                _currentSaveData.DungeonRun.CurrentFloor = Mathf.Max(1, dungeonFloor);
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

            int unlockGroupCount = normalizedFloor / DungeonStartFloorUnlockInterval;
            for (int groupIndex = 1; groupIndex <= unlockGroupCount; groupIndex++)
            {
                int startFloor = groupIndex * DungeonStartFloorUnlockInterval + 1;
                UnlockDungeonStartFloorInternal(startFloor);
            }
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

        public SaveData CreateNewSaveData()
        {
            SaveData data = new SaveData();
            EnsureSaveDataValid(data);
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

        private string GetSaveFolderPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, SAVE_FOLDER);
        }

        private string GetSavePath(int index)
        {
            return System.IO.Path.Combine(
                GetSaveFolderPath(),
                $"{index}_v{CURRENT_SAVE_VERSION}.json");
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

        private void EnsureSaveDataValid(SaveData data)
        {
            data.Global ??= new GlobalData();
            data.Variables ??= new SaveVariableData();
            data.Location ??= new SaveLocationData();
            data.Location.DungeonFloor = Mathf.Max(1, data.Location.DungeonFloor);

            data.Town ??= new TownData();
            EnsureTownDataValid(data.Town);

            if (data.DungeonRun != null)
            {
                EnsureDungeonRunDataValid(data.DungeonRun, data.Town.Character);
            }
            else if (data.Location.AreaType == SaveAreaType.Dungeon)
            {
                data.DungeonRun = CreateDungeonRunFromPersistent(data.Town.Character, data.Location.DungeonFloor);
            }

            EnsureDungeonStartFloorUnlocksInitialized(data);
        }

        private void EnsureTownDataValid(TownData data)
        {
            if (data == null)
                return;

            data.Stash ??= new StashData();
            data.Character ??= new CharacterData();
            EnsureStashDataValid(data.Stash);
            EnsureCharacterDataValid(data.Character);
        }

        private void EnsureStashDataValid(StashData data)
        {
            if (data == null)
                return;

            data.Items ??= new List<InventoryItemData>();
            if (data.Capacity <= 0)
                data.Capacity = GetGameConfig().InitialStashSize;
        }

        private void EnsureCharacterDataValid(CharacterData data)
        {
            if (data == null)
                return;

            data.Equipment ??= new EquipmentData();
            data.Skills ??= new SkillCData();
            data.Skills.EnsureValid();
            data.Backpack ??= new BackpackData();
            data.Backpack.Items ??= new List<InventoryItemData>();
            if (data.Backpack.Capacity <= 0)
                data.Backpack.Capacity = Mathf.Max(1, GetGameConfig().InitialBackpackSize);

            data.Props ??= new CharacterPropData();
            data.Props.EnsureValid(GetPropSlotCount(), GetPropShortcutSlotCount());
            PropInventoryUtility.MigrateBackpackPropsToPropSlots(data);
        }

        private void EnsureDungeonRunDataValid(DungeonRunData data, CharacterData fallbackCharacter = null)
        {
            if (data == null)
                return;

            data.CurrentFloor = Mathf.Max(1, data.CurrentFloor);
            data.Character ??= CloneCharacterData(fallbackCharacter);
            EnsureCharacterDataValid(data.Character);
            data.Monsters ??= new List<MonsterStateData>();
            data.ItemDrops ??= new List<ItemDropData>();
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
            EnsureDungeonRunDataValid(data, sourceCharacter);
            return data;
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
            _currentSaveData ??= new SaveData();
            EnsureSaveDataValid(_currentSaveData);
        }

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
