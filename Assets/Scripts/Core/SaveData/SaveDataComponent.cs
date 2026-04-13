using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 存档系统组件
    /// </summary>
    public class SaveDataComponent : GameComponent<SaveDataComponent>
    {
        // ========== 配置 ==========
        public override int Priority => 18;

        private const string SAVE_FOLDER = "SaveData";
        private const int CURRENT_SAVE_VERSION = 1;
        private const int CURRENT_CONTENT_VERSION = 1;
        private const int MAX_SAVES = 20;

        // ========== 状态 ==========
        private SaveData _currentSaveData;
        private string _currentSavePath;

        // ========== 事件 ==========
        public event Action<SaveData> OnSaveSuccess;
        public event Action<string> OnSaveFailed;
        public event Action<SaveData> OnLoadSuccess;
        public event Action<string> OnLoadFailed;

        // ========== 生命周期 ==========
        public override void Initialize()
        {
            base.Initialize();
            EnsureSaveFolderExists();
            Debug.Log("[SaveDataComponent] Initialized");
        }

        public override void Cleanup()
        {
            base.Cleanup();
        }

        // ========== 公开 API ==========

        /// <summary>
        /// 保存当前存档（覆盖现有存档）
        /// 若无当前存档，则保存到 "autosave"
        /// </summary>
        public bool Save()
        {
            if (_currentSaveData == null)
            {
                _currentSaveData = new SaveData();
            }

            string saveName = GetCurrentSaveName();
            return SaveToSlot(saveName);
        }

        /// <summary>
        /// 保存到指定存档位置
        /// </summary>
        public bool SaveToSlot(string slotName)
        {
            if (_currentSaveData == null)
            {
                _currentSaveData = new SaveData();
            }

            try
            {
                // 校验数据
                EnsureSaveDataValid(_currentSaveData);

                // 填充元数据
                _currentSaveData.SaveName = slotName;
                _currentSaveData.SaveTimestamp = DateTime.Now.Ticks;
                _currentSaveData.GameVersion = Application.version;

                // 序列化
                string json = JsonUtility.ToJson(_currentSaveData, true);

                // 写入文件（确保目录存在）
                string filePath = GetSavePath(slotName);
                EnsureSaveFolderExists();
                System.IO.File.WriteAllText(filePath, json);

                // 创建备份
                CreateBackup(filePath);

                _currentSavePath = filePath;

                OnSaveSuccess?.Invoke(_currentSaveData);
                Debug.Log($"[SaveDataComponent] Game saved to slot: {slotName}");
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
        /// 读取当前存档，若无当前存档则读取 "autosave"
        /// </summary>
        public bool Load()
        {
            string saveName = GetCurrentSaveName();
            return LoadFromSlot(saveName);
        }

        /// <summary>
        /// 从指定存档位置读取
        /// </summary>
        public bool LoadFromSlot(string slotName)
        {
            try
            {
                string filePath = GetSavePath(slotName);

                if (!System.IO.File.Exists(filePath))
                {
                    OnLoadFailed?.Invoke($"Save file not found: {slotName}");
                    return false;
                }

                string json = System.IO.File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    OnLoadFailed?.Invoke($"Failed to parse save file: {slotName}");
                    return false;
                }

                _currentSaveData = data;
                _currentSavePath = filePath;

                OnLoadSuccess?.Invoke(data);
                Debug.Log($"[SaveDataComponent] Game loaded from slot: {slotName}");

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
        /// 获取所有存档记录（最多20条）
        /// </summary>
        public List<SaveRecord> GetAllSaveRecords()
        {
            List<SaveRecord> records = new();

            try
            {
                string folderPath = GetSaveFolderPath();

                if (!System.IO.Directory.Exists(folderPath))
                    return records;

                var files = System.IO.Directory.GetFiles(folderPath, "*.json");

                // 排序：最新的在前
                System.Array.Sort(files, (a, b) =>
                    System.IO.File.GetLastWriteTime(b).CompareTo(System.IO.File.GetLastWriteTime(a))
                );

                int count = 0;
                foreach (var file in files)
                {
                    // 过滤掉备份文件
                    if (file.EndsWith(".backup.json"))
                        continue;

                    if (count >= MAX_SAVES)
                        break;

                    try
                    {
                        string json = System.IO.File.ReadAllText(file);
                        SaveData data = JsonUtility.FromJson<SaveData>(json);

                        if (data != null)
                        {
                            string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                            records.Add(new SaveRecord
                            {
                                SlotName = fileName,
                                SaveName = data.SaveName,
                                Timestamp = data.SaveTimestamp,
                                GameVersion = data.GameVersion,
                            });
                            count++;
                        }
                    }
                    catch
                    {
                        // 忽略无法解析的文件
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
        /// 删除指定存档
        /// </summary>
        public bool DeleteSlot(string slotName)
        {
            try
            {
                string filePath = GetSavePath(slotName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    Debug.Log($"[SaveDataComponent] Save deleted: {slotName}");
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

        /// <summary>
        /// 获取当前内存中的存档数据
        /// </summary>
        public SaveData GetCurrentSaveData()
        {
            return _currentSaveData;
        }

        /// <summary>
        /// 创建新的空存档数据
        /// </summary>
        public SaveData CreateNewSaveData()
        {
            return new SaveData();
        }

        // ========== 内部方法 ==========

        private string GetSaveFolderPath()
        {
            return System.IO.Path.Combine(
                Application.persistentDataPath,
                SAVE_FOLDER
            );
        }

        private string GetSavePath(string slotName)
        {
            return System.IO.Path.Combine(
                GetSaveFolderPath(),
                $"{slotName}_v{CURRENT_SAVE_VERSION}.json"
            );
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
        /// <summary>
        /// 创建备份文件
        /// </summary>
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

        private string GetCurrentSaveName()
        {
            return _currentSavePath != null 
                ? System.IO.Path.GetFileNameWithoutExtension(_currentSavePath).Replace($"_v{CURRENT_SAVE_VERSION}", "")
                : "autosave";
        }
    }

    /// <summary>
    /// 存档记录信息
    /// </summary>
    [System.Serializable]
    public class SaveRecord
    {
        public string SlotName;            // 存档槽位名称
        public string SaveName;            // 存档显示名称
        public long Timestamp;             // 存档时间戳
        public string GameVersion;         // 游戏版本
        public int MaxFloor;               // 最深层数
        public int TotalRuns;              // 总游玩局数

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
    /// 根据此信息决定进入城镇还是地牢流程
    /// </summary>
    public class LoadGameContext
    {
        /// <summary>
        /// 读取的存档数据
        /// </summary>
        public SaveData SaveData;

        /// <summary>
        /// 存档槽位名称
        /// </summary>
        public string SlotName;

        /// <summary>
        /// 是否有未完成的地牢当局
        /// 为 true 时应进入地牢流程，false 时进入城镇流程
        /// </summary>
        public bool HasDungeonRun;

        /// <summary>
        /// 当前地牢层数（仅当 HasDungeonRun 为 true 时有效）
        /// </summary>
        public int DungeonFloor;

        /// <summary>
        /// 判断是否应进入地牢流程
        /// </summary>
        public bool ShouldEnterDungeon()
        {
            return HasDungeonRun;
        }

        /// <summary>
        /// 判断是否应进入城镇流程
        /// </summary>
        public bool ShouldEnterTown()
        {
            return !ShouldEnterDungeon();
        }
    }
}
