using System;
using System.Collections.Generic;
using UnityEngine;

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
        public const string EquipmentDataChangedEventName = "SaveData.Town.Character.Equipment.Changed";
        public const string SkillDataChangedEventName = "SaveData.Town.Character.Skill.Changed";

        public override int Priority => 18;

        private const string SAVE_FOLDER = "SaveData";
        private const int CURRENT_SAVE_VERSION = 1;
        private const int CURRENT_CONTENT_VERSION = 1;
        private const int MAX_SAVES = 20;
        private const int DEFAULT_SAVE_INDEX = 0;

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

                    if (count >= MAX_SAVES)
                        break;

                    try
                    {
                        string json = System.IO.File.ReadAllText(file);
                        SaveData data = JsonUtility.FromJson<SaveData>(json);
                        if (data == null)
                            continue;

                        records.Add(new SaveRecord
                        {
                            SaveIndex = data.SaveIndex,
                            Timestamp = data.SaveTimestamp,
                            GameVersion = data.GameVersion,
                            StashMoney = data.Town?.StashMoney ?? 0,
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
            return _currentSaveData.Town.Stash;
        }

        public CharacterData GetCharacterData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town.Character;
        }

        public EquipmentData GetEquipmentData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town.Character.Equipment;
        }

        public SkillCData GetSkillData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town.Character.Skills;
        }

        public BackpackData GetBackpackData()
        {
            EnsureCurrentSaveDataValid();
            return _currentSaveData.Town.Character.Backpack;
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
            EventComponent.Instance.Publish(new CommonGameEvent(TownDataChangedEventName, _currentSaveData.Town));
            NotifySaveDataChanged();
        }

        public void NotifyStashDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(StashDataChangedEventName, _currentSaveData.Town.Stash));
            NotifyTownDataChanged();
        }

        public void NotifyCharacterDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterDataChangedEventName, _currentSaveData.Town.Character));
            NotifyTownDataChanged();
        }

        public void NotifyBackpackDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(BackpackDataChangedEventName, _currentSaveData.Town.Character.Backpack));
            NotifyCharacterDataChanged();
        }

        public void NotifyEquipmentDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(EquipmentDataChangedEventName, _currentSaveData.Town.Character.Equipment));
            NotifyCharacterDataChanged();
        }

        public void NotifySkillDataChanged()
        {
            EnsureCurrentSaveDataValid();
            EventComponent.Instance.Publish(new CommonGameEvent(SkillDataChangedEventName, _currentSaveData.Town.Character.Skills));
            NotifyCharacterDataChanged();
        }

        public SaveData CreateNewSaveData()
        {
            SaveData data = new SaveData();
            EnsureSaveDataValid(data);
            return data;
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
            if (data.Global == null)
            {
                data.Global = new GlobalData();
            }

            if (data.Variables == null)
            {
                data.Variables = new SaveVariableData();
            }

            if (data.Town == null)
            {
                data.Town = new TownData();
            }

            if (data.Town.Stash == null)
            {
                data.Town.Stash = new StashData();
            }
            else if (data.Town.Stash.Items == null)
            {
                data.Town.Stash.Items = new List<InventoryItemData>();
            }

            if (data.Town.Character == null)
            {
                data.Town.Character = new CharacterData();
            }

            if (data.Town.Character.Equipment == null)
            {
                data.Town.Character.Equipment = new EquipmentData();
            }

            if (data.Town.Character.Skills == null)
            {
                data.Town.Character.Skills = new SkillCData();
            }
            else if (data.Town.Character.Skills.Chains == null || data.Town.Character.Skills.Chains.Length != 5)
            {
                data.Town.Character.Skills = new SkillCData();
            }

            if (data.Town.Character.Backpack == null)
            {
                data.Town.Character.Backpack = new BackpackData();
            }
            else if (data.Town.Character.Backpack.Items == null)
            {
                data.Town.Character.Backpack.Items = new List<InventoryItemData>();
            }

            data.Town.Character.MigrateLegacyData();
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
            EventComponent.Instance.Publish(new CommonGameEvent(TownDataChangedEventName, _currentSaveData.Town));
            EventComponent.Instance.Publish(new CommonGameEvent(StashDataChangedEventName, _currentSaveData.Town.Stash));
            EventComponent.Instance.Publish(new CommonGameEvent(CharacterDataChangedEventName, _currentSaveData.Town.Character));
            EventComponent.Instance.Publish(new CommonGameEvent(BackpackDataChangedEventName, _currentSaveData.Town.Character.Backpack));
            EventComponent.Instance.Publish(new CommonGameEvent(EquipmentDataChangedEventName, _currentSaveData.Town.Character.Equipment));
            EventComponent.Instance.Publish(new CommonGameEvent(SkillDataChangedEventName, _currentSaveData.Town.Character.Skills));
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
        public bool HasDungeonRun;
        public int DungeonFloor;

        public bool ShouldEnterDungeon()
        {
            return HasDungeonRun;
        }

        public bool ShouldEnterTown()
        {
            return !ShouldEnterDungeon();
        }
    }
}
