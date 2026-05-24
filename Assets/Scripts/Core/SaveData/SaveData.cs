using System;
using System.Collections.Generic;
using UnityEngine;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core {
    /// <summary>
    /// 完整存档数据容器
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
        public DungeonRunData DungeonRun;
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
        public CharacterPropData Props;

        public CharacterData()
        {
            Equipment = new EquipmentData();
            Skills = new SkillCData();
            Backpack = new BackpackData();
            Props = new CharacterPropData();
        }

    }
    /// <summary>
    /// 仓库数据
    /// </summary>
    [System.Serializable]
    public class StashData
    {
        public int Capacity = -1;
        /// <summary>
        /// 物品列表
        /// </summary>
        public List<InventoryItemData> Items = new();
    }

    [System.Serializable]
    public class BackpackData
    {
        public int Capacity;
        public List<InventoryItemData> Items = new();
    }

    [System.Serializable]
    public class CharacterPropData
    {
        public List<CharacterPropSlotData> Slots = new();
        public int[] ShortcutSlotIndexes = Array.Empty<int>();

        public void EnsureValid(int slotCount, int shortcutSlotCount)
        {
            Slots ??= new List<CharacterPropSlotData>();

            int clampedSlotCount = Math.Max(0, slotCount);
            while (Slots.Count < clampedSlotCount)
            {
                Slots.Add(new CharacterPropSlotData());
            }

            while (Slots.Count > clampedSlotCount)
            {
                Slots.RemoveAt(Slots.Count - 1);
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                Slots[i] ??= new CharacterPropSlotData();
                Slots[i].EnsureValid();
            }

            int clampedShortcutCount = Math.Max(0, shortcutSlotCount);
            if (ShortcutSlotIndexes == null || ShortcutSlotIndexes.Length != clampedShortcutCount)
            {
                int[] resizedShortcuts = new int[clampedShortcutCount];
                for (int i = 0; i < resizedShortcuts.Length; i++)
                {
                    resizedShortcuts[i] = i < clampedSlotCount ? i : -1;
                }

                if (ShortcutSlotIndexes != null)
                {
                    int copyCount = Math.Min(ShortcutSlotIndexes.Length, resizedShortcuts.Length);
                    for (int i = 0; i < copyCount; i++)
                    {
                        resizedShortcuts[i] = ShortcutSlotIndexes[i];
                    }
                }

                ShortcutSlotIndexes = resizedShortcuts;
            }

            for (int i = 0; i < ShortcutSlotIndexes.Length; i++)
            {
                if (ShortcutSlotIndexes[i] < -1 || ShortcutSlotIndexes[i] >= clampedSlotCount)
                    ShortcutSlotIndexes[i] = -1;
            }
        }

        public void ClearSlots()
        {
            if (Slots == null)
                return;

            for (int i = 0; i < Slots.Count; i++)
            {
                Slots[i]?.Clear();
            }
        }
    }

    [System.Serializable]
    public class CharacterPropSlotData
    {
        public int ItemId = -1;
        public int Quantity;

        public bool IsEmpty => ItemId < 0 || Quantity <= 0;

        public void EnsureValid()
        {
            if (ItemId < 0 || Quantity <= 0)
                Clear();
        }

        public void Clear()
        {
            ItemId = -1;
            Quantity = 0;
        }
    }
    #endregion

    #region 战斗数据
    /// <summary>
    /// 地牢当局数据
    /// </summary>
    [System.Serializable]
    public class DungeonRunData
    {
        public string RunId;
        public long RunTimestamp;
        public int CurrentFloor;
        public int Seed;
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
    /// 装备系统数据
    /// </summary>
    [System.Serializable]
    public class EquipmentData
    {
        public int MagicStoneId;
        public int[] SpiritSlots = new int[4];

        public EquipmentData()
        {
            MagicStoneId = -1;
            for (int i = 0; i < 4; i++)
            {
                SpiritSlots[i] = -1;
            }
        }
    }

    /// <summary>
    /// 单个物品数据（支持堆叠）
    /// </summary>
    [System.Serializable]
    public class InventoryItemData
    {
        public int ItemId;
        public int Quantity;
        public ItemType ItemType;
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

        public void EnsureValid()
        {
            if (Chains == null)
            {
                Chains = new SkillChainData[5];
            }
            else if (Chains.Length != 5)
            {
                SkillChainData[] resizedChains = new SkillChainData[5];
                int copyCount = Math.Min(Chains.Length, resizedChains.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    resizedChains[i] = Chains[i];
                }

                Chains = resizedChains;
            }

            for (int i = 0; i < Chains.Length; i++)
            {
                Chains[i] ??= new SkillChainData();
                Chains[i].Index = i;
                Chains[i].EnsureSlots();
            }
        }
    }

    /// <summary>
    /// 单个技能链数据
    /// </summary>
    [System.Serializable]
    public class SkillChainData
    {
        public int Index;
        public List<SkillChainSlotData> Slots = new();

        public void EnsureSlots()
        {
            Slots ??= new List<SkillChainSlotData>();

            for (int i = 0; i < Slots.Count; i++)
            {
                Slots[i] ??= new SkillChainSlotData();
            }
        }

    }

    /// <summary>
    /// 单个技能链槽位
    /// </summary>
    [System.Serializable]
    public class SkillChainSlotData
    {
        public int SkillStoneItemId = -1;
        public int SkillAdditionId = -1;
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
