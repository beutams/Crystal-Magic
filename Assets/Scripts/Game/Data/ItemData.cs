using System.Collections.Generic;
using CrystalMagic.Core;
using Newtonsoft.Json;

namespace CrystalMagic.Game.Data
{
    public enum ItemType
    {
        [EditorLabel("无")]
        None = 0,
        [EditorLabel("技能石")]
        SkillStone = 1,
        [EditorLabel("道具")]
        Prop = 2,
        [EditorLabel("魔法石")]
        MagicStone = 3,
        [EditorLabel("精灵")]
        Spirit = 4,
    }

    [System.Serializable]
    public class ItemData : DataRow
    {
        public string NameKey;
        public string DescriptionKey;
        public ItemType ItemType;

        [JsonIgnore]
        public string Name => LocalizationComponent.Resolve(NameKey);

        [JsonIgnore]
        public string Description => LocalizationComponent.Resolve(DescriptionKey);

        /// <summary>
        /// 额外关联数据的 Id。
        /// </summary>
        public int ExtraId = -1;

        public int Rarity;
        public int MaxStack;
        public int SellPrice;
        public string IconPath;
        public bool IsNonTransferable;
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
