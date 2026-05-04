using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    /// <summary>物品类型（与 ItemData 表一致）</summary>
    public enum ItemType
    {
        None = 0,  //普通道具
        Consumable = 1, //消耗品
        SkillStone = 2,  //技能石
        Item = 3,  //普通物品
        Weapon = 4,  //武器
        Accessory = 5,  //饰品
    }

    /// <summary>
    /// 物品配置表行
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
        /// <summary>
        /// 鍙€夌殑棰濆鍏宠仈 Id銆?
        /// SkillStone -> SkillId
        /// Weapon/Accessory -> BuffId
        /// </summary>
        public int ExtraId;

        /// <summary>稀有度</summary>
        public int Rarity;

        /// <summary>最大叠加数量</summary>
        public int MaxStack;

        /// <summary>出售价格</summary>
        public int SellPrice;

        /// <summary>图标资源路径</summary>
        public string IconPath;
    }
}
