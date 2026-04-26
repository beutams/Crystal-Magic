namespace CrystalMagic.UI
{
    public sealed class StashUIModel : UIModelBase
    {
        public const string DataChangedEventName = "StashUIModel.DataChanged";

        private readonly StashInventoryDisplayData[] _inventoryItems = new StashInventoryDisplayData[32];
        private readonly System.Collections.Generic.List<StashItemDisplayData> _stashItems = new();

        private int _inventorySlotCount = 32;
        private StashCategory _category = StashCategory.All;
        private long _stashMoney;

        public StashInventoryDisplayData[] InventoryItems => _inventoryItems;
        public System.Collections.Generic.IReadOnlyList<StashItemDisplayData> StashItems => _stashItems;
        public int InventorySlotCount => _inventorySlotCount;
        public StashCategory Category => _category;
        public long StashMoney => _stashMoney;

        public void SetCategory(StashCategory category)
        {
            if (_category == category)
                return;

            _category = category;
            Refresh();
        }

        public void Refresh()
        {
            RefreshInventory();
            RefreshStash();
            RefreshMoney();
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }

        private void RefreshInventory()
        {
            System.Array.Clear(_inventoryItems, 0, _inventoryItems.Length);

            CrystalMagic.Core.BackpackData backpackData = CrystalMagic.Core.SaveDataComponent.Instance.GetBackpackData();
            System.Collections.Generic.List<CrystalMagic.Core.InventoryItemData> backpackItems = backpackData?.Items;
            if (backpackItems == null)
                return;

            int count = backpackItems.Count < _inventorySlotCount ? backpackItems.Count : _inventorySlotCount;
            for (int i = 0; i < count; i++)
            {
                CrystalMagic.Core.InventoryItemData inventoryItem = backpackItems[i];
                if (inventoryItem == null)
                    continue;

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(inventoryItem.ItemId);
                _inventoryItems[i] = new StashInventoryDisplayData
                {
                    ItemId = inventoryItem.ItemId,
                    Count = inventoryItem.Quantity,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                };
            }
        }

        private void RefreshStash()
        {
            _stashItems.Clear();

            CrystalMagic.Core.StashData stashData = CrystalMagic.Core.SaveDataComponent.Instance.GetStashData();
            System.Collections.Generic.List<CrystalMagic.Core.InventoryItemData> stashItems = stashData?.Items;
            if (stashItems == null)
                return;

            System.Collections.Generic.List<CrystalMagic.Core.InventoryItemData> sortedItems = new(stashItems);
            sortedItems.Sort((a, b) =>
            {
                if (a == null && b == null)
                    return 0;
                if (a == null)
                    return 1;
                if (b == null)
                    return -1;
                return a.ItemId.CompareTo(b.ItemId);
            });

            for (int i = 0; i < sortedItems.Count; i++)
            {
                CrystalMagic.Core.InventoryItemData stashItem = sortedItems[i];
                if (stashItem == null)
                    continue;

                CrystalMagic.Game.Data.ItemData itemData = CrystalMagic.Core.DataComponent.Instance.Get<CrystalMagic.Game.Data.ItemData>(stashItem.ItemId);
                CrystalMagic.Game.Data.ItemType itemType = itemData != null ? itemData.ItemType : stashItem.ItemType;
                if (!MatchesCategory(itemType))
                    continue;

                _stashItems.Add(new StashItemDisplayData
                {
                    ItemId = stashItem.ItemId,
                    Count = stashItem.Quantity,
                    ItemType = itemType,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                });
            }
        }

        private void RefreshMoney()
        {
            _stashMoney = CrystalMagic.Core.SaveDataComponent.Instance.GetTownData()?.StashMoney ?? 0;
        }

        private bool MatchesCategory(CrystalMagic.Game.Data.ItemType itemType)
        {
            switch (_category)
            {
                case StashCategory.Skill:
                    return itemType == CrystalMagic.Game.Data.ItemType.SkillStone;
                case StashCategory.Equip:
                    return itemType == CrystalMagic.Game.Data.ItemType.Weapon
                        || itemType == CrystalMagic.Game.Data.ItemType.Accessory;
                case StashCategory.Props:
                    return itemType != CrystalMagic.Game.Data.ItemType.SkillStone
                        && itemType != CrystalMagic.Game.Data.ItemType.Weapon
                        && itemType != CrystalMagic.Game.Data.ItemType.Accessory;
                default:
                    return true;
            }
        }
    }

    public enum StashCategory
    {
        All = 0,
        Skill = 1,
        Equip = 2,
        Props = 3,
    }

    public sealed class StashInventoryDisplayData
    {
        public int ItemId;
        public int Count;
        public string Name;
        public string IconPath;
    }

    public sealed class StashItemDisplayData
    {
        public int ItemId;
        public int Count;
        public CrystalMagic.Game.Data.ItemType ItemType;
        public string Name;
        public string IconPath;
    }
}
