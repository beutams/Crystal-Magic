using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Core
{
    public static class InventoryUtility
    {
        public static bool SortBackpack(BackpackData backpackData)
        {
            if (backpackData == null)
                return false;

            EnsureBackpackSlots(backpackData);
            return SortItems(backpackData.Items, true, id => DataComponent.Instance.Get<ItemData>(id));
        }

        public static bool SortStash(StashData stashData)
        {
            return stashData?.Items != null &&
                   SortItems(stashData.Items, false, id => DataComponent.Instance.Get<ItemData>(id));
        }

        // Keep slot capacity, quantities and unknown items intact. Never touch equipped items.
        // The resolver also makes these rules testable without a running game world.
        public static bool SortItems(List<InventoryItemData> items, bool keepEmptySlots,
            System.Func<int, ItemData> resolveItem)
        {
            if (items == null)
                return false;

            int slotCount = items.Count;
            var definitions = new Dictionary<int, ItemData>();
            var sorted = new List<InventoryItemData>(slotCount);
            foreach (InventoryItemData item in items)
            {
                if (item == null || item.IsEmpty)
                    continue;
                if (!definitions.ContainsKey(item.ItemId))
                    definitions.Add(item.ItemId, resolveItem(item.ItemId));
                sorted.Add(new InventoryItemData
                {
                    ItemId = item.ItemId, Quantity = item.Quantity, ItemType = item.ItemType,
                });
            }

            sorted.Sort((a, b) =>
            {
                ItemData da = definitions[a.ItemId], db = definitions[b.ItemId];
                int order = (da?.ItemType ?? a.ItemType).CompareTo(db?.ItemType ?? b.ItemType);
                if (order != 0) return order;
                order = (db?.Rarity ?? 0).CompareTo(da?.Rarity ?? 0);
                if (order != 0) return order;
                order = a.ItemId.CompareTo(b.ItemId);
                if (order != 0) return order;
                order = a.ItemType.CompareTo(b.ItemType);
                return order != 0 ? order : b.Quantity.CompareTo(a.Quantity);
            });

            var compacted = new List<InventoryItemData>(slotCount);
            InventoryItemData partial = null;
            foreach (InventoryItemData item in sorted)
            {
                int maxStack = System.Math.Max(1, definitions[item.ItemId]?.MaxStack ?? 1);
                if (partial != null && partial.ItemId == item.ItemId && partial.ItemType == item.ItemType)
                {
                    int moved = System.Math.Min(item.Quantity, System.Math.Max(0, maxStack - partial.Quantity));
                    partial.Quantity += moved;
                    item.Quantity -= moved;
                }
                if (item.Quantity > 0)
                {
                    compacted.Add(item);
                    partial = item;
                }
            }
            if (keepEmptySlots)
                while (compacted.Count < slotCount) compacted.Add(new InventoryItemData());

            bool changed = items.Count != compacted.Count;
            for (int i = 0; !changed && i < items.Count; i++)
            {
                InventoryItemData old = items[i], next = compacted[i];
                changed = old == null || old.ItemId != next.ItemId || old.Quantity != next.Quantity ||
                          old.ItemType != next.ItemType;
            }
            if (!changed)
                return false;

            items.Clear();
            items.AddRange(compacted);
            return true;
        }

        public static void EnsureBackpackSlots(BackpackData backpackData)
        {
            if (backpackData == null)
                return;

            backpackData.Items ??= new List<InventoryItemData>();
            int slotCount = Mathf.Max(1, backpackData.Capacity);
            if (backpackData.Items.Count > slotCount)
                backpackData.Capacity = backpackData.Items.Count;

            if (backpackData.Capacity < slotCount)
                backpackData.Capacity = slotCount;

            slotCount = Mathf.Max(slotCount, backpackData.Items.Count);
            for (int i = 0; i < backpackData.Items.Count; i++)
            {
                InventoryItemData item = backpackData.Items[i];
                if (item == null)
                {
                    backpackData.Items[i] = new InventoryItemData();
                    continue;
                }

                if (item.IsEmpty)
                    item.Clear();
            }

            while (backpackData.Items.Count < slotCount)
                backpackData.Items.Add(new InventoryItemData());
        }

        public static int AddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
            if (backpackData == null)
                return 0;

            EnsureBackpackSlots(backpackData);
            if (itemId < 0 || quantity <= 0)
                return 0;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            ItemType itemType = itemData != null ? itemData.ItemType : ItemType.None;
            int remaining = quantity;

            for (int i = 0; i < backpackData.Items.Count && remaining > 0; i++)
            {
                InventoryItemData item = backpackData.Items[i];
                if (item.IsEmpty || item.ItemId != itemId || item.Quantity >= maxStack)
                    continue;

                int addCount = Mathf.Min(maxStack - item.Quantity, remaining);
                item.Quantity += addCount;
                remaining -= addCount;
            }

            for (int i = 0; i < backpackData.Items.Count && remaining > 0; i++)
            {
                InventoryItemData item = backpackData.Items[i];
                if (!item.IsEmpty)
                    continue;

                int addCount = Mathf.Min(maxStack, remaining);
                item.ItemId = itemId;
                item.Quantity = addCount;
                item.ItemType = itemType;
                remaining -= addCount;
            }

            return quantity - remaining;
        }

        public static bool CanAddItemToBackpack(BackpackData backpackData, int itemId, int quantity)
        {
            if (backpackData == null)
                return false;

            EnsureBackpackSlots(backpackData);
            return quantity > 0 && GetAvailableAddCountInBackpack(backpackData, itemId) >= quantity;
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

            EnsureBackpackSlots(backpackData);
            if (itemId < 0)
                return 0;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            long available = 0;
            for (int i = 0; i < backpackData.Items.Count; i++)
            {
                InventoryItemData item = backpackData.Items[i];
                if (item.IsEmpty)
                    available += maxStack;
                else if (item.ItemId == itemId && item.Quantity < maxStack)
                    available += maxStack - item.Quantity;
            }

            return available > int.MaxValue ? int.MaxValue : (int)available;
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
                if (inventoryItem != null && !inventoryItem.IsEmpty && inventoryItem.ItemId == itemId)
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
                inventoryItem.Clear();

            return true;
        }

        public static bool TryMoveBackpackSlot(BackpackData backpackData, int sourceSlotIndex, int targetSlotIndex)
        {
            EnsureBackpackSlots(backpackData);
            if (!IsValidBackpackSlot(backpackData, sourceSlotIndex) ||
                !IsValidBackpackSlot(backpackData, targetSlotIndex) ||
                sourceSlotIndex == targetSlotIndex)
                return false;

            InventoryItemData source = backpackData.Items[sourceSlotIndex];
            InventoryItemData target = backpackData.Items[targetSlotIndex];
            if (source.IsEmpty)
                return false;

            if (target.IsEmpty)
            {
                backpackData.Items[targetSlotIndex] = source;
                backpackData.Items[sourceSlotIndex] = new InventoryItemData();
                return true;
            }

            if (source.ItemId != target.ItemId)
            {
                backpackData.Items[targetSlotIndex] = source;
                backpackData.Items[sourceSlotIndex] = target;
                return true;
            }

            ItemData itemData = DataComponent.Instance.Get<ItemData>(source.ItemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            int moveCount = Mathf.Min(source.Quantity, Mathf.Max(0, maxStack - target.Quantity));
            if (moveCount <= 0)
                return false;

            target.Quantity += moveCount;
            source.Quantity -= moveCount;
            if (source.Quantity <= 0)
                source.Clear();
            return true;
        }

        public static bool TryAddItemToBackpackSlot(
            BackpackData backpackData,
            int targetSlotIndex,
            int itemId,
            int quantity,
            ItemType fallbackItemType = ItemType.None)
        {
            EnsureBackpackSlots(backpackData);
            if (!IsValidBackpackSlot(backpackData, targetSlotIndex) || itemId < 0 || quantity <= 0)
                return false;

            InventoryItemData target = backpackData.Items[targetSlotIndex];
            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            int maxStack = itemData != null && itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            if (!target.IsEmpty && target.ItemId != itemId)
                return false;
            if ((!target.IsEmpty ? target.Quantity : 0) + quantity > maxStack)
                return false;

            if (target.IsEmpty)
            {
                target.ItemId = itemId;
                target.ItemType = itemData != null ? itemData.ItemType : fallbackItemType;
            }
            target.Quantity += quantity;
            return true;
        }

        public static bool IsValidBackpackSlot(BackpackData backpackData, int slotIndex)
        {
            return backpackData?.Items != null && slotIndex >= 0 && slotIndex < backpackData.Items.Count;
        }
    }
}
