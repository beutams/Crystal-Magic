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
        public override string ChangedEventName => DataChangedEventName;

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
            Quantity = 1;
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
            if (MaxBuyCount <= 0)
                return 0;

            if (quantity < 1)
                return 1;

            if (quantity > MaxBuyCount)
                return MaxBuyCount;

            return quantity;
        }

        public int GetMaxBuyCountForCurrentMoney()
        {
            return GetMaxBuyCount();
        }

        private int GetMaxBuyCount()
        {
            int inventoryLimitedCount = InventoryUtility.GetAvailableAddCountInCharacterInventory(
                SaveDataComponent.Instance.GetBackpackData(),
                SaveDataComponent.Instance.GetCharacterPropData(),
                ItemId);

            if (Price == 0)
                return inventoryLimitedCount;

            if (Price < 0)
                return 0;

            long money = SaveDataComponent.Instance.GetTownData()?.StashMoney ?? 0;
            long maxBuyCount = money / Price;
            if (maxBuyCount <= 0)
                return 0;

            int moneyLimitedCount = maxBuyCount > int.MaxValue ? int.MaxValue : (int)maxBuyCount;

            return UnityEngine.Mathf.Min(moneyLimitedCount, inventoryLimitedCount);
        }

        private int GetHaveCount(int itemId)
        {
            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            CharacterPropData propData = SaveDataComponent.Instance.GetCharacterPropData();
            return InventoryUtility.GetItemCountInCharacterInventory(backpackData, propData, itemId);
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
