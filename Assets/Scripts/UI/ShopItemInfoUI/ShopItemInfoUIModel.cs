namespace CrystalMagic.UI
{
    public sealed class ShopItemInfoUIOpenData
    {
        public string Name;
        public int HaveCount;
        public string Description;
        public int Price;
        public string IconPath;
    }

    public sealed class ShopItemInfoUIModel : UIModelBase, IUIOpenDataReceiver<ShopItemInfoUIOpenData>
    {
        public const string DataChangedEventName = "ShopItemInfoUIModel.DataChanged";

        public string Name { get; private set; }
        public int HaveCount { get; private set; }
        public string Description { get; private set; }
        public int Price { get; private set; }
        public string IconPath { get; private set; }

        public void SetOpenData(ShopItemInfoUIOpenData data)
        {
            Name = data != null ? data.Name : string.Empty;
            HaveCount = data != null ? data.HaveCount : 0;
            Description = data != null ? data.Description : string.Empty;
            Price = data != null ? data.Price : 0;
            IconPath = data != null ? data.IconPath : string.Empty;
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }
    }
}
