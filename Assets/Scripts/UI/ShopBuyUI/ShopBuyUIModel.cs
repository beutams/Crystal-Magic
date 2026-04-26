using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class ShopBuyUIOpenData
    {
        public int ItemId;
        public string Name;
        public int HaveCount;
        public string Description;
        public int Price;
        public string IconPath;
    }

    public sealed class ShopBuyUIModel : UIModelBase, IUIOpenDataReceiver<ShopBuyUIOpenData>
    {
        public const string DataChangedEventName = "ShopBuyUIModel.DataChanged";

        public int ItemId { get; private set; }
        public string Name { get; private set; }
        public int HaveCount { get; private set; }
        public string Description { get; private set; }
        public int Price { get; private set; }
        public string IconPath { get; private set; }
        public int Quantity { get; private set; }
        public int MaxBuyCount { get; private set; }

        public void SetOpenData(ShopBuyUIOpenData data)
        {
            ItemId = data != null ? data.ItemId : 0;
            Name = data != null ? data.Name : string.Empty;
            HaveCount = data != null ? data.HaveCount : 0;
            Description = data != null ? data.Description : string.Empty;
            Price = data != null ? data.Price : 0;
            IconPath = data != null ? data.IconPath : string.Empty;
            Quantity = 0;
            RefreshRuntimeData();
        }

        public void RefreshRuntimeData()
        {
            HaveCount = GetHaveCount(ItemId);
            MaxBuyCount = GetMaxBuyCount();
            Quantity = ClampQuantity(Quantity);
            PublishChanged();
        }

        public void SetQuantity(int quantity)
        {
            int clampedQuantity = ClampQuantity(quantity);
            if (Quantity == clampedQuantity)
            {
                PublishChanged();
                return;
            }

            Quantity = clampedQuantity;
            PublishChanged();
        }

        public void AddQuantity(int delta)
        {
            SetQuantity(Quantity + delta);
        }

        private int ClampQuantity(int quantity)
        {
            if (quantity < 0)
                return 0;

            if (quantity > MaxBuyCount)
                return MaxBuyCount;

            return quantity;
        }

        private int GetMaxBuyCount()
        {
            if (Price <= 0)
                return 0;

            long money = SaveDataComponent.Instance.GetTownData()?.StashMoney ?? 0;
            long maxBuyCount = money / Price;
            if (maxBuyCount <= 0)
                return 0;

            return maxBuyCount > int.MaxValue ? int.MaxValue : (int)maxBuyCount;
        }

        private int GetHaveCount(int itemId)
        {
            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            if (backpackData?.Items == null)
                return 0;

            int count = 0;
            for (int i = 0; i < backpackData.Items.Count; i++)
            {
                InventoryItemData item = backpackData.Items[i];
                if (item == null || item.ItemId != itemId)
                    continue;

                count += item.Quantity;
            }

            return count;
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
