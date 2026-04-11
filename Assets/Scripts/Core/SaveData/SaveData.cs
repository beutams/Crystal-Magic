using System;
using System.Collections.Generic;
using UnityEngine;
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
        public string SaveName;                    // 存档名称
        public int SaveVersion = 1;                // 存档版本（用于迁移）
        public int ContentVersion = 1;             // 内容版本（与游戏版本对齐）
        public long SaveTimestamp;                 // 存档时间戳
        public string GameVersion;                 // 游戏版本号

        // ========== 全局数据（跨城镇和地牢） ==========
        /// <summary>
        /// 玩家全局成就和进度数据
        /// 见框架设计文档第 8 章
        /// </summary>
        public GlobalData Global;

        // ========== 城镇数据 ==========
        /// <summary>
        /// 城镇状态数据
        /// 包含：仓库、货币、角色数据（装备、技能、背包）
        /// </summary>
        public TownData Town;

        // ========== 地牢数据（当局可选保存） ==========
        /// <summary>
        /// 当局地牢数据（仅地牢内有效）
        /// 包含：地牢层数、种子、玩家位置、怪物位置、角色当前状态
        /// </summary>
        public DungeonRunData DungeonRun;

        public SaveData()
        {
            Global = new GlobalData();
            Town = new TownData();
        }
    }

    // ========== 全局数据 ==========

    /// <summary>
    /// 全局数据 - 成就和进度
    /// 跨城镇和地牢持久化
    /// </summary>
    [System.Serializable]
    public class GlobalData
    {
        /// <summary>
        /// 玩家全局成就数据
        /// </summary>
        public AchievementData Achievements;

        /// <summary>
        /// 玩家进度统计
        /// </summary>
        public PlayerProgressData Progress;

        public GlobalData()
        {
            Achievements = new AchievementData();
            Progress = new PlayerProgressData();
        }
    }

    /// <summary>
    /// 成就数据
    /// </summary>
    [System.Serializable]
    public class AchievementData
    {
        public int MaxFloorReached;            // 最深到达层数
        public int TotalRunCount;              // 总游玩局数
        public int TotalDeathCount;            // 总死亡次数
        public Dictionary<int, int> BossKillCounts = new(); // Boss 击杀统计 {BossId: Count}
        public long TotalPlayTimeSeconds;      // 总游玩时间（秒）
    }

    /// <summary>
    /// 玩家进度数据
    /// </summary>
    [System.Serializable]
    public class PlayerProgressData
    {
        // TODO: 补充玩家进度相关字段
        // 如：解锁的技能、装备、角色等
    }

    // ========== 城镇数据 ==========

    /// <summary>
    /// 城镇数据 - 仓库、货币、角色信息
    /// 见框架设计文档第 4 章
    /// </summary>
    [System.Serializable]
    public class TownData
    {
        /// <summary>
        /// 仓库数据 - 无限容量
        /// </summary>
        public StashData Stash;

        /// <summary>
        /// 仓库货币
        /// </summary>
        public long StashMoney;

        /// <summary>
        /// 角色数据（城镇状态）
        /// 包含：装备、技能配置、背包
        /// </summary>
        public CharacterData Character;

        public TownData()
        {
            Stash = new StashData();
            Character = new CharacterData();
        }
    }

    /// <summary>
    /// 角色数据
    /// 在城镇和地牢中都有对应实例
    /// </summary>
    [System.Serializable]
    public class CharacterData
    {
        /// <summary>
        /// 角色装备系统（法杖 + 4增益槽）
        /// 见游戏设计文档第 5 章
        /// </summary>
        public EquipmentData Equipment;

        /// <summary>
        /// 技能配置 - 包含5组技能链
        /// 见游戏设计文档第 6 章
        /// </summary>
        public SkillCData Skills;

        /// <summary>
        /// 角色背包 - 城镇中持有
        /// 进地牢时创建快照
        /// </summary>
        public List<InventoryItemData> BackpackItems = new();

        /// <summary>
        /// 角色位置和朝向
        /// 在城镇时可能为固定值，在地牢时实时保存
        /// </summary>
        public PositionData Position;

        public CharacterData()
        {
            Equipment = new EquipmentData();
            Skills = new SkillCData();
            Position = new PositionData();
        }
    }

    /// <summary>
    /// 仓库数据 - 无限容量存储
    /// 见框架设计文档第 4 章
    /// </summary>
    [System.Serializable]
    public class StashData
    {
        /// <summary>
        /// 物品列表（堆叠后）
        /// 包括：道具、技能石、材料等
        /// </summary>
        public List<InventoryItemData> Items = new();
    }

    // ========== 地牢数据 ==========

    /// <summary>
    /// 地牢当局数据
    /// 仅当处于地牢时存在，回城后可清空
    /// 见框架设计文档第 1.3 节 RunSession
    /// </summary>
    [System.Serializable]
    public class DungeonRunData
    {
        // ========== 地牢基本信息 ==========
        public string RunId;                   // 本局唯一 Id（调试用）
        public long RunTimestamp;              // 本局开始时间
        public int CurrentFloor;               // 当前层数
        public int Seed;                       // 地牢种子（用于重现地图）

        // ========== 玩家当前状态 ==========
        /// <summary>
        /// 玩家在地牢中的角色状态
        /// 包含：装备、技能、背包、位置等
        /// </summary>
        public CharacterData Character;

        /// <summary>
        /// 当局货币
        /// 见游戏设计文档第 3.5 节
        /// </summary>
        public long RunMoney;

        // ========== 地牢环境数据 ==========
        /// <summary>
        /// 怪物位置和状态列表
        /// TODO: 等待 AI 系统确定怪物状态结构
        /// </summary>
        public List<MonsterStateData> Monsters = new();

        /// <summary>
        /// 物品掉落位置
        /// TODO: 等待物理系统确定物品状态结构
        /// </summary>
        public List<ItemDropData> ItemDrops = new();

        // TODO: 已探索房间列表
        // TODO: 宝箱状态
        // TODO: 传送点
        // TODO: Boss 房状态
    }

    /// <summary>
    /// 位置和朝向数据
    /// </summary>
    [System.Serializable]
    public class PositionData
    {
        public float X;                        // X 坐标
        public float Y;                        // Y 坐标
        public float Z;                        // Z 坐标
        public float Rotation;                 // 朝向角度
    }

    /// <summary>
    /// 怪物状态数据
    /// TODO: 补充具体怪物状态字段
    /// </summary>
    [System.Serializable]
    public class MonsterStateData
    {
        public int MonsterId;                  // 怪物唯一 Id（当局）
        public int MonsterDefId;               // 怪物配置 Id
        public float X;                        // X 坐标
        public float Y;                        // Y 坐标
        public float Z;                        // Z 坐标
        public float HP;                       // 当前 HP
        public float MaxHP;                    // 最大 HP
        // TODO: 补充：状态、Buff、仇恨列表等
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

    // ========== 基础数据类型 ==========

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
    /// 技能数据 - 包含5组技能链
    /// 见游戏设计文档第 6 章
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
}
