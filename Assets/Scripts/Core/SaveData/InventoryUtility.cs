using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Core
{
    public static class InventoryUtility
    {
        public static int AddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
            if (backpackData == null)
                return 0;

            backpackData.Items ??= new List<InventoryItemData>();
            return AddItem(backpackData.Items, backpackData.Capacity, itemId, quantity, ItemType.None);
        }

        public static bool CanAddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
            if (backpackData == null)
                return false;

            backpackData.Items ??= new List<InventoryItemData>();
            return CanAddItem(backpackData.Items, backpackData.Capacity, itemId, quantity);
        }

        public static int AddItemToCharacterInventory(BackpackData backpackData, CharacterPropData propData, int itemId, int quantity)
        {
            return AddItemToBackpack(backpackData, itemId, quantity);
        }

        public static bool CanAddItemToCharacterInventory(BackpackData backpackData, CharacterPropData propData, int itemId, int quantity)
        {
            return CanAddItemToBackpack(backpackData, itemId, quantity);
        }

        public static int GetItemCountInCharacterInventory(BackpackData backpackData, CharacterPropData propData, int itemId)
        {
            int backpackCount = GetItemCount(backpackData?.Items, itemId);
            int propCount = PropInventoryUtility.IsPropItem(itemId)
                ? PropInventoryUtility.GetItemCount(propData, itemId)
                : 0;
            return backpackCount + propCount;
        }

        public static int GetAvailableAddCountInCharacterInventory(BackpackData backpackData, CharacterPropData propData, int itemId)
        {
            return GetAvailableAddCountInBackpack(backpackData, itemId);
        }

        public static int AddItem(List<InventoryItemData> items, int capacity, int itemId, int quantity, ItemType fallbackItemType)
        {
            if (items == null || itemId < 0 || quantity <= 0)
                return 0;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            ItemType itemType = itemData != null ? itemData.ItemType : fallbackItemType;
            int remaining = quantity;

            for (int i = 0; i < items.Count && remaining > 0; i++)
            {
                InventoryItemData inventoryItem = items[i];
                if (inventoryItem == null || inventoryItem.ItemId != itemId || inventoryItem.Quantity >= maxStack)
                    continue;

                int addCount = Mathf.Min(maxStack - inventoryItem.Quantity, remaining);
                inventoryItem.Quantity += addCount;
                remaining -= addCount;
            }

            int slotLimit = capacity > 0 ? capacity : int.MaxValue;
            while (remaining > 0 && items.Count < slotLimit)
            {
                int addCount = Mathf.Min(maxStack, remaining);
                items.Add(new InventoryItemData
                {
                    ItemId = itemId,
                    Quantity = addCount,
                    ItemType = itemType,
                });
                remaining -= addCount;
            }

            return quantity - remaining;
        }

        public static bool CanAddItem(List<InventoryItemData> items, int capacity, int itemId, int quantity)
        {
            return quantity > 0 && GetAvailableAddCount(items, capacity, itemId) >= quantity;
        }

        public static int GetAvailableAddCountInBackpack(BackpackData backpackData, int itemId)
        {
            if (backpackData == null)
                return 0;

            backpackData.Items ??= new List<InventoryItemData>();
            return GetAvailableAddCount(backpackData.Items, backpackData.Capacity, itemId);
        }

        public static int GetAvailableAddCount(List<InventoryItemData> items, int capacity, int itemId)
        {
            if (items == null || itemId < 0)
                return 0;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            long available = 0;

            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemData inventoryItem = items[i];
                if (inventoryItem == null || inventoryItem.ItemId != itemId || inventoryItem.Quantity >= maxStack)
                    continue;

                available += maxStack - inventoryItem.Quantity;
            }

            int slotLimit = capacity > 0 ? capacity : int.MaxValue;
            int freeSlots = Mathf.Max(0, slotLimit - items.Count);
            available += (long)freeSlots * maxStack;

            return available > int.MaxValue ? int.MaxValue : (int)available;
        }

        public static int GetItemCount(List<InventoryItemData> items, int itemId)
        {
            if (items == null || itemId < 0)
                return 0;

            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemData inventoryItem = items[i];
                if (inventoryItem != null && inventoryItem.ItemId == itemId && inventoryItem.Quantity > 0)
                    count += inventoryItem.Quantity;
            }

            return count;
        }

        public static int FindFirstItemSlot(BackpackData backpackData, int itemId)
        {
            if (backpackData?.Items == null || itemId < 0)
                return -1;

            for (int i = 0; i < backpackData.Items.Count; i++)
            {
                InventoryItemData inventoryItem = backpackData.Items[i];
                if (inventoryItem != null && inventoryItem.ItemId == itemId && inventoryItem.Quantity > 0)
                    return i;
            }

            return -1;
        }

        public static bool TryConsumeBackpackItem(BackpackData backpackData, int slotIndex, int itemId, int count)
        {
            if (backpackData?.Items == null || count <= 0 || slotIndex < 0 || slotIndex >= backpackData.Items.Count)
                return false;

            InventoryItemData inventoryItem = backpackData.Items[slotIndex];
            if (inventoryItem == null || inventoryItem.ItemId != itemId || inventoryItem.Quantity < count)
                return false;

            inventoryItem.Quantity -= count;
            if (inventoryItem.Quantity <= 0)
                backpackData.Items.RemoveAt(slotIndex);

            return true;
        }
    }
}
