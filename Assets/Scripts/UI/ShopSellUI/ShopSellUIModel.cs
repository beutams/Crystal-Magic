using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class ShopSellUIOpenData
    {
        public int SlotIndex;
        public int ItemId;
        public string Name;
        public int HaveCount;
        public string Description;
        public int Price;
        public string IconPath;
    }

    public sealed class ShopSellUIModel : UIModelBase, IUIOpenDataReceiver<ShopSellUIOpenData>
    {
        public const string DataChangedEventName = "ShopSellUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        public int SlotIndex { get; private set; }
        public int ItemId { get; private set; }
        public string Name { get; private set; }
        public int HaveCount { get; private set; }
        public string Description { get; private set; }
        public int Price { get; private set; }
        public string IconPath { get; private set; }
        public int Quantity { get; private set; }
        public int MaxSellCount { get; private set; }

        public void SetOpenData(ShopSellUIOpenData data)
        {
            SlotIndex = data != null ? data.SlotIndex : -1;
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
            HaveCount = GetSlotCount();
            MaxSellCount = HaveCount > 0 ? HaveCount : 0;
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

            if (quantity > MaxSellCount)
                return MaxSellCount;

            return quantity;
        }

        private int GetSlotCount()
        {
            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            if (backpackData?.Items == null || SlotIndex < 0 || SlotIndex >= backpackData.Items.Count)
                return 0;

            InventoryItemData item = backpackData.Items[SlotIndex];
            if (item == null || item.ItemId != ItemId)
                return 0;

            return item.Quantity;
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
