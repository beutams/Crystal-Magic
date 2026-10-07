using System;
using System.Collections.Generic;
using UnityEngine;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core {
    /// <summary>
    /// 完整存档数据容器
    /// </summary>
    [System.Serializable]
    public class SaveData : ISerializationCallbackReceiver
    {
        // ========== 元数据 ==========
        public int SaveIndex;                    // 存档名称
        public string SaveGuid;                  // 存档唯一身份，不随槽位编号或进程变化
        public string LastBattleSettlementId;

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

        // JsonUtility expands a null inline class into a default-valued object.
        // Preserve whether a player snapshot actually existed before serialization.
        [SerializeField] private int _playerSnapshotVersion;
        [SerializeField] private bool _hasPlayerSnapshot;

        public void OnBeforeSerialize()
        {
            _playerSnapshotVersion = 1;
            _hasPlayerSnapshot = Player != null;
        }

        public void OnAfterDeserialize()
        {
            if (_playerSnapshotVersion > 0)
            {
                if (!_hasPlayerSnapshot)
                    Player = null;
                return;
            }

            // Older fresh Town saves wrote this exact placeholder for a null player.
            // Keep real Town positions and combat snapshots, including zero health.
            if (Location?.AreaType == SaveAreaType.Town && Player != null &&
                Player.SaveId == -1 && Player.UnitDataId == -1 && Player.Faction == UnitFactionType.Player &&
                Player.X == 0f && Player.Y == 0f && Player.Z == 0f &&
                Player.Health == 0f && Player.Mana == 0f)
                Player = null;
        }
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
        /// <summary>
        /// 绑定的账号 ID（Steam 或本地测试账号）；字段名保留以兼容现有存档。
        /// </summary>
        public ulong SteamAccountId;

        /// <summary>
        /// 创建或载入存档时绑定的玩家昵称。
        /// </summary>
        public string Name = string.Empty;

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

        public void EnsureValid(int slotCount, List<string> repairedPaths = null, string path = "Props")
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
        // Persist the acquisition journal across floors and save/load. Older saves lack the flag.
        public bool HasAcquisitionHistory;
        public List<InventoryItemData> AcquiredItems = new();
        public long AcquiredMoney;
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
        public int ItemId = -1;
        public int Quantity;
        public ItemType ItemType;

        public bool IsEmpty => ItemId < 0 || Quantity <= 0;

        public void Clear()
        {
            ItemId = -1;
            Quantity = 0;
            ItemType = ItemType.None;
        }
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
        public const int MaxLength = 10;
        [Newtonsoft.Json.JsonIgnore]
        public bool IsFull => (Slots?.Count ?? 0) >= MaxLength;

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
