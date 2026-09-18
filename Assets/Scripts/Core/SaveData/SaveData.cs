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
        public string SaveGuid;                  // 存档唯一身份，不随槽位编号或进程变化

        // ========== 全局数据 ==========
        /// <summary>
        /// 玩家全局成就和进度数据
        /// </summary>
        public GlobalData Global;
        public SaveVariableData Variables = new();
        public SaveLocationData Location = new();

        // ========== GameWorld 投影 ==========
        public StashData Stash;
        public CharacterData Character;
        public UnitRuntimeData Player;
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

    #region 角色与仓库数据
    /// <summary>
    /// 角色数据
    /// </summary>
    [System.Serializable]
    public class CharacterData
    {
        public long Money;
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
        public long Money;
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

        public void EnsureValid(int slotCount, int shortcutSlotCount, List<string> repairedPaths = null, string path = "Props")
        {
            if (Slots == null)
            {
                Slots = new List<CharacterPropSlotData>();
                repairedPaths?.Add($"{path}.Slots");
            }

            int clampedSlotCount = Math.Max(0, slotCount);
            while (Slots.Count < clampedSlotCount)
            {
                Slots.Add(new CharacterPropSlotData());
                repairedPaths?.Add($"{path}.Slots[{Slots.Count - 1}]");
            }

            while (Slots.Count > clampedSlotCount)
            {
                Slots.RemoveAt(Slots.Count - 1);
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == null)
                {
                    Slots[i] = new CharacterPropSlotData();
                    repairedPaths?.Add($"{path}.Slots[{i}]");
                }
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
                repairedPaths?.Add($"{path}.ShortcutSlotIndexes");
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
        public int BaseSeed;
        public int ThemeId = -1;
        public int CurrentFloor;
        public int Seed;
        public List<UnitRuntimeData> Units = new();
        /// <summary>
        /// 物品掉落位置
        /// </summary>
        public List<ItemDropData> ItemDrops = new();
    }

    /// <summary>
    /// 场景单位在保存瞬间的运行状态。
    /// </summary>
    [System.Serializable]
    public class UnitRuntimeData
    {
        public int SaveId = -1;
        public int UnitDataId = -1;
        public UnitFactionType Faction;
        public float X;
        public float Y;
        public float Z;
        public float Health;
        public float Mana;
    }

    /// <summary>
    /// 物品掉落数据
    /// </summary>
    [System.Serializable]
    public class ItemDropData
    {
        public DropRewardType DropType;
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
        public EquipmentPropertyData Properties;

        public EquipmentData()
        {
            MagicStoneId = -1;
            for (int i = 0; i < 4; i++)
            {
                SpiritSlots[i] = -1;
            }
        }
    }

    [System.Serializable]
    public struct EquipmentPropertyData
    {
        public float MoveSpeed;
        public float MaxHealth;
        public float Defense;
        public float AttackPower;
        public float SkillRange;
        public float MaxMp;
        public float HealthRegen;
        public float MpRegen;
        public float ChantSpeed;
        public float WaterPower;
        public float FirePower;
        public float LightningPower;
        public float WindPower;
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

        public void EnsureValid(List<string> repairedPaths = null, string path = "Skills")
        {
            if (Chains == null)
            {
                Chains = new SkillChainData[5];
                repairedPaths?.Add($"{path}.Chains");
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
                repairedPaths?.Add($"{path}.Chains");
            }

            for (int i = 0; i < Chains.Length; i++)
            {
                if (Chains[i] == null)
                {
                    Chains[i] = new SkillChainData();
                    repairedPaths?.Add($"{path}.Chains[{i}]");
                }
                Chains[i].Index = i;
                Chains[i].EnsureSlots(repairedPaths, $"{path}.Chains[{i}]");
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

        public void EnsureSlots(List<string> repairedPaths = null, string path = "Skills.Chain")
        {
            if (Slots == null)
            {
                Slots = new List<SkillChainSlotData>();
                repairedPaths?.Add($"{path}.Slots");
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == null)
                {
                    Slots[i] = new SkillChainSlotData();
                    repairedPaths?.Add($"{path}.Slots[{i}]");
                }
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
        public int DungeonThemeId = -1;
        public int DungeonFloor = 1;
    }
}
