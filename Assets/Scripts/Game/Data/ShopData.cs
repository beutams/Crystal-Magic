using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    /// <summary>
    /// 商店配置表行
    /// </summary>
    [System.Serializable]
    public class ShopData : DataRow
    {
        public int itemDataId;

        public int Price;

        public int Grade;

        public string NPC;
    }
}