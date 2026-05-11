using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    public enum ItemType
    {
        None = 0,
        Consumable = 1,
        SkillStone = 2,
        Item = 3,
        Weapon = 4,
        Accessory = 5,
    }

    [System.Serializable]
    public class ItemData : DataRow
    {
        public string Name;
        public string Description;
        public ItemType ItemType;

        /// <summary>
        /// 额外关联数据的 Id。
        /// </summary>
        public int ExtraId;

        public int Rarity;
        public int MaxStack;
        public int SellPrice;
        public string IconPath;
    }

    [System.Serializable]
    public struct EquipPropertyEntry
    {
        public PropertyModifierChannel Channel;
        public float BaseBonus;
    }

    [ReadOnlyData]
    [System.Serializable]
    public class EquipData : DataRow
    {
        public List<EquipPropertyEntry> Properties = new();
    }
}
