using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 存档系统组件
    /// </summary>
    public class SaveDataComponent : GameComponent<SaveDataComponent>
    {
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

                _currentSaveData = data;
                _currentSaveIndex = data.SaveIndex;

                OnLoadSuccess?.Invoke(data);
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
            return _currentSaveData;
        }

        public SaveData CreateNewSaveData()
        {
            return new SaveData();
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

            if (data.Town == null)
            {
                data.Town = new TownData();
            }
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
