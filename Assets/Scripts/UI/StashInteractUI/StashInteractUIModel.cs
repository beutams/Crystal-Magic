namespace CrystalMagic.UI
{
    public enum StashInteractMode
    {
        Store = 0,
        Withdraw = 1,
    }

    public sealed class StashInteractUIOpenData
    {
        public StashInteractMode Mode;
        public int SourceSlotIndex;
        public int ItemId;
        public string Name;
        public int HaveCount;
        public string Description;
        public string IconPath;
    }

    public sealed class StashInteractUIModel : UIModelBase, IUIOpenDataReceiver<StashInteractUIOpenData>
    {
        public const string DataChangedEventName = "StashInteractUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        public StashInteractMode Mode { get; private set; }
        public int SourceSlotIndex { get; private set; }
        public int ItemId { get; private set; }
        public string Name { get; private set; }
        public int HaveCount { get; private set; }
        public string Description { get; private set; }
        public string IconPath { get; private set; }
        public int Quantity { get; private set; }
        public int MaxCount { get; private set; }

        public void SetOpenData(StashInteractUIOpenData data)
        {
            Mode = data != null ? data.Mode : StashInteractMode.Store;
            SourceSlotIndex = data != null ? data.SourceSlotIndex : -1;
            ItemId = data != null ? data.ItemId : 0;
            Name = data != null ? data.Name : string.Empty;
            HaveCount = data != null ? data.HaveCount : 0;
            Description = data != null ? data.Description : string.Empty;
            IconPath = data != null ? data.IconPath : string.Empty;
            Quantity = 1;
            RefreshRuntimeData();
        }

        public void RefreshRuntimeData()
        {
            HaveCount = GetSourceCount();
            MaxCount = GetMaxTransferCount(HaveCount);
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
            if (MaxCount <= 0)
                return 0;

            if (quantity < 1)
                return 1;

            if (quantity > MaxCount)
                return MaxCount;

            return quantity;
        }

        public int GetCurrentMaxCount()
        {
            return MaxCount;
        }

        private int GetMaxTransferCount(int sourceCount)
        {
            if (sourceCount <= 0)
                return 0;

            if (Mode == StashInteractMode.Store)
                return sourceCount;

            if (CrystalMagic.Core.PropInventoryUtility.IsPropItem(ItemId))
            {
                int availableCount = CrystalMagic.Core.PropInventoryUtility.GetAvailableAddCount(
                    CrystalMagic.Core.SaveDataComponent.Instance.GetCharacterPropData(),
                    ItemId);
                return UnityEngine.Mathf.Min(sourceCount, availableCount);
            }

            int backpackAvailableCount = CrystalMagic.Core.InventoryUtility.GetAvailableAddCountInBackpack(
                CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData(),
                ItemId);
            return UnityEngine.Mathf.Min(sourceCount, backpackAvailableCount);
        }

        private int GetSourceCount()
        {
            CrystalMagic.Core.InventoryItemData sourceItem = GetSourceItem();
            if (sourceItem == null || sourceItem.ItemId != ItemId)
                return 0;

            return sourceItem.Quantity;
        }

        private CrystalMagic.Core.InventoryItemData GetSourceItem()
        {
            System.Collections.Generic.List<CrystalMagic.Core.InventoryItemData> items = Mode == StashInteractMode.Store
                ? CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData()?.Items
                : CrystalMagic.Core.SaveDataComponent.Instance.GetStashData()?.Items;

            if (items == null || SourceSlotIndex < 0 || SourceSlotIndex >= items.Count)
                return null;

            return items[SourceSlotIndex];
        }

        private void PublishChanged()
        {
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }
    }
}
