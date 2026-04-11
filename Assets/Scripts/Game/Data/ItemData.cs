using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    /// <summary>物品类型（与 ItemData 表一致）</summary>
    public enum ItemType
    {
        None = 0,
        Consumable = 1,
        SkillStone = 2,
        Material = 3,
        KeyItem = 4,
    }

    /// <summary>
    /// 物品配置表行
    /// 对应存档中 InventoryItemData.ItemId / ItemDropData.ItemId
    /// JSON：Assets/Res/Data/ItemDataTable.json
    /// </summary>
    [System.Serializable]
    public class ItemData : DataRow
    {
        /// <summary>物品名称</summary>
        public string Name;

        /// <summary>物品描述</summary>
        public string Description;

        /// <summary>物品类型</summary>
        public ItemType ItemType;

        /// <summary>稀有度：1=普通 2=精良 3=稀有 4=史诗 5=传说</summary>
        public int Rarity;

        /// <summary>最大叠加数量</summary>
        public int MaxStack;

        /// <summary>出售价格（0 表示不可出售）</summary>
        public int SellPrice;

        /// <summary>图标资源路径（相对 Resources/）</summary>
        public string IconPath;
    }
}
