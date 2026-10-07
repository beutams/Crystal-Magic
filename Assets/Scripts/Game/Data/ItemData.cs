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

        [JsonIgnore]
        public string TypeName => ItemType switch
        {
            ItemType.SkillStone => LocalizationComponent.Resolve("item.type.skill_stone"),
            ItemType.Prop => LocalizationComponent.Resolve("item.type.prop"),
            ItemType.MagicStone => LocalizationComponent.Resolve("item.type.magic_stone"),
            ItemType.Spirit => LocalizationComponent.Resolve("item.type.spirit"),
            _ => string.Empty,
        };

        /// <summary>用于物品介绍的类型标签和说明，保留原始 Description 不变。</summary>
        [JsonIgnore]
        public string DescriptionWithType
        {
            get
            {
                string description = Description;
                string typeName = TypeName;
                if (string.IsNullOrEmpty(typeName))
                    return description;

                return string.IsNullOrWhiteSpace(description)
                    ? $"【{typeName}】"
                    : $"【{typeName}】{description}";
            }
        }

        /// <summary>
        /// 额外关联数据的 Id。
        /// </summary>
        public int ExtraId = -1;

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

    [System.Serializable]
    public class EquipData : DataRow
    {
        public List<EquipPropertyEntry> Properties = new();
    }
}
