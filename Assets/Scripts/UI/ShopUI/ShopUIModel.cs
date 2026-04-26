using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class ShopUIModel : UIModelBase, IUIOpenDataReceiver<string>
    {
        public const string DataChangedEventName = "ShopUIModel.DataChanged";

        private readonly List<ShopCommodityDisplayData> _commodities = new();
        private readonly ShopInventoryDisplayData[] _inventoryItems = new ShopInventoryDisplayData[32];

        private string _npcName;
        private int _inventorySlotCount = 32;
        private long _money;

        public IReadOnlyList<ShopCommodityDisplayData> Commodities => _commodities;
        public ShopInventoryDisplayData[] InventoryItems => _inventoryItems;
        public int InventorySlotCount => _inventorySlotCount;
        public string NpcName => _npcName;
        public long Money => _money;

        public void SetOpenData(string data)
        {
            _npcName = data;
        }

        public void Refresh()
        {
            RefreshCommodities();
            RefreshInventory();
            RefreshMoney();
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }

        private void RefreshCommodities()
        {
            _commodities.Clear();

            List<ShopData> shopDataList = new(DataComponent.Instance.FindAll<ShopData>(data => data.NPC == _npcName));
            shopDataList.Sort((a, b) => a.Id.CompareTo(b.Id));

            foreach (ShopData shopData in shopDataList)
            {
                ItemData itemData = DataComponent.Instance.Get<ItemData>(shopData.itemDataId);
                _commodities.Add(new ShopCommodityDisplayData
                {
                    ShopDataId = shopData.Id,
                    ItemId = shopData.itemDataId,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    Description = itemData != null ? itemData.Description : string.Empty,
                    Price = shopData.Price,
                    Grade = shopData.Grade,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                });
            }
        }

        private void RefreshInventory()
        {
            System.Array.Clear(_inventoryItems, 0, _inventoryItems.Length);

            BackpackData backpackData = SaveDataComponent.Instance.GetBackpackData();
            List<InventoryItemData> backpackItems = backpackData?.Items;
            if (backpackItems == null)
                return;

            int count = backpackItems.Count < _inventorySlotCount ? backpackItems.Count : _inventorySlotCount;
            for (int i = 0; i < count; i++)
            {
                InventoryItemData inventoryItem = backpackItems[i];
                if (inventoryItem == null)
                    continue;

                ItemData itemData = DataComponent.Instance.Get<ItemData>(inventoryItem.ItemId);
                _inventoryItems[i] = new ShopInventoryDisplayData
                {
                    SlotIndex = i,
                    ItemId = inventoryItem.ItemId,
                    Count = inventoryItem.Quantity,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    Description = itemData != null ? itemData.Description : string.Empty,
                    SellPrice = itemData != null ? itemData.SellPrice : 0,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                };
            }
        }

        private void RefreshMoney()
        {
            TownData townData = SaveDataComponent.Instance.GetTownData();
            _money = townData?.StashMoney ?? 0;
        }
    }

    public sealed class ShopCommodityDisplayData
    {
        public int ShopDataId;
        public int ItemId;
        public string Name;
        public string Description;
        public int Price;
        public int Grade;
        public string IconPath;
    }

    public sealed class ShopInventoryDisplayData
    {
        public int SlotIndex;
        public int ItemId;
        public int Count;
        public string Name;
        public string Description;
        public int SellPrice;
        public string IconPath;
    }
}
