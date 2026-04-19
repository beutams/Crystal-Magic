using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core {
    /// <summary>
    /// 完整存档数据容器
    /// 分三层结构：全局数据、城镇数据、地牢数据
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        // ========== 元数据 ==========
        public int SaveIndex;                    // 存档名称
        public long SaveTimestamp;                 // 存档时间戳
        public string GameVersion;                 // 游戏版本号

        // ========== 全局数据 ==========
        /// <summary>
        /// 玩家全局成就和进度数据
        /// </summary>
        public GlobalData Global;
        public SaveVariableData Variables = new();
        public SaveLocationData Location = new();

        // ========== 城镇数据 ==========
        /// <summary>
        /// 城镇状态数据
        /// </summary>
        public TownData Town;
    }
    /// <summary>
    /// 全局数据
    /// </summary>
    [System.Serializable]
    public class GlobalData
    {
        public long TotalPlayTimeSeconds;      // 总游玩时间（秒）
    }

    #region 城镇数据
    /// <summary>
    /// 城镇数据
    /// </summary>
    [System.Serializable]
    public class TownData
    {
        /// <summary>
        /// 仓库数据
        /// </summary>
        public StashData Stash;
        /// <summary>
        /// 仓库货币
        /// </summary>
        public long StashMoney;
        /// <summary>
        /// 角色数据
        /// </summary>
        public CharacterData Character;
        public TownData()
        {
            Stash = new StashData();
            Character = new CharacterData();
            StashMoney = 0;
        }
    }

    /// <summary>
    /// 角色数据
    /// </summary>
    [System.Serializable]
    public class CharacterData
    {
        [SerializeField]
        [FormerlySerializedAs("SelectedSkillChainIndex")]
        private int _legacySelectedSkillChainIndex = -1;
        [SerializeField]
        [FormerlySerializedAs("BackpackItems")]
        private List<InventoryItemData> _legacyBackpackItems;
        /// <summary>
        /// 角色装备系统
        /// </summary>
        public EquipmentData Equipment;
        /// <summary>
        /// 技能配置
        /// </summary>
        public SkillCData Skills;
        /// <summary>
        /// 角色背包
        /// </summary>
        public BackpackData Backpack;

        public CharacterData()
        {
            Equipment = new EquipmentData();
            Skills = new SkillCData();
            Backpack = new BackpackData();
        }

        public void MigrateLegacyData()
        {
            if (_legacyBackpackItems != null && _legacyBackpackItems.Count > 0)
            {
                Backpack ??= new BackpackData();
                Backpack.Items ??= new List<InventoryItemData>();

                if (Backpack.Items.Count == 0)
                {
                    Backpack.Items.AddRange(_legacyBackpackItems);
                }

                _legacyBackpackItems = null;
            }
        }

        public int ConsumeLegacySelectedSkillChainIndex(int defaultValue = 0)
        {
            int selectedSkillChainIndex = _legacySelectedSkillChainIndex >= 0 ? _legacySelectedSkillChainIndex : defaultValue;
            _legacySelectedSkillChainIndex = -1;
            return selectedSkillChainIndex;
        }
    }
    /// <summary>
    /// 仓库数据
    /// </summary>
    [System.Serializable]
    public class StashData
    {
        /// <summary>
        /// 物品列表
        /// </summary>
        public List<InventoryItemData> Items = new();
    }

    [System.Serializable]
    public class BackpackData
    {
        public List<InventoryItemData> Items = new();
    }
    #endregion

    #region 战斗数据
    /// <summary>
    /// 地牢当局数据
    /// </summary>
    [System.Serializable]
    public class DungeonRunData
    {
        public string RunId;                   // 本局唯一 Id（调试用）
        public long RunTimestamp;              // 本局开始时间
        public int CurrentFloor;               // 当前层数
        public int Seed;                       // 地牢种子（用于重现地图）
        /// <summary>
        /// 玩家在地牢中的角色状态
        /// </summary>
        public CharacterData Character;
        /// <summary>
        /// 当局货币
        /// </summary>
        public long RunMoney;
        /// <summary>
        /// 怪物位置和状态列表
        /// </summary>
        public List<MonsterStateData> Monsters = new();
        /// <summary>
        /// 物品掉落位置
        /// </summary>
        public List<ItemDropData> ItemDrops = new();
    }

    /// <summary>
    /// 怪物状态数据
    /// </summary>
    [System.Serializable]
    public class MonsterStateData
    {
        public int MonsterId;                  // 怪物唯一 Id（当局）
        public int MonsterDefId;               // 怪物配置 Id
        public float X;                        // X 坐标
        public float Y;                        // Y 坐标
        public float HP;                       // 当前 HP
        public float MaxHP;                    // 最大 HP
    }

    /// <summary>
    /// 物品掉落数据
    /// </summary>
    [System.Serializable]
    public class ItemDropData
    {
        public int ItemId;                     // 物品 Id
        public int Quantity;                   // 数量
        public float X;                        // X 坐标
        public float Y;                        // Y 坐标
        public float Z;                        // Z 坐标
    }
    #endregion

    #region 基础数据
    /// <summary>
    /// 装备系统数据（包括法杖 + 4个增益槽）
    /// 见游戏设计文档第 5 章
    /// </summary>
    [System.Serializable]
    public class EquipmentData
    {
        public int StaffId;                    // 法杖 Id（0 表示未装备）
        public int StaffLevel;                 // 法杖强化等级
        public int[] BonusSlots = new int[4]; // 增益 Id 数组，-1 表示空槽

        public EquipmentData()
        {
            StaffId = 0;
            StaffLevel = 0;
            for (int i = 0; i < 4; i++)
            {
                BonusSlots[i] = -1;
            }
        }
    }

    /// <summary>
    /// 单个物品数据（支持堆叠）
    /// </summary>
    [System.Serializable]
    public class InventoryItemData
    {
        public int ItemId;                     // 物品 Id
        public int Quantity;                   // 数量（支持堆叠）
        public ItemType ItemType;              // 物品类型
    }

    /// <summary>
    /// 技能数据
    /// </summary>
    [System.Serializable]
    public class SkillCData
    {
        public SkillChainData[] Chains = new SkillChainData[5];

        public SkillCData()
        {
            for (int i = 0; i < 5; i++)
            {
                Chains[i] = new SkillChainData { Index = i };
            }
        }
    }

    /// <summary>
    /// 单个技能链数据
    /// </summary>
    [System.Serializable]
    public class SkillChainData
    {
        public int Index;                                        // 0-4 对应数字键 1-5
        public List<int> SkillStoneIds = new();                 // 链上技能石 Id 列表（按顺序）
        public List<SkillEffectData>[] Effects = new List<SkillEffectData>[0]; // 每颗石上的特效修饰
    }

    /// <summary>
    /// 技能石特效数据
    /// 不包含等级字段，特效由 Id 唯一确定
    /// </summary>
    [System.Serializable]
    public class SkillEffectData
    {
        public int EffectId;
    }
    #endregion

    public enum SaveAreaType
    {
        Town = 0,
        Training = 1,
        Dungeon = 2,
    }

    [System.Serializable]
    public class SaveLocationData
    {
        public SaveAreaType AreaType = SaveAreaType.Town;
        public int DungeonFloor = 1;
    }
}
