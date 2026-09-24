using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Core
{
    public static class PropInventoryUtility
    {
        public static bool IsPropItem(int itemId)
        {
            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            return itemData != null && itemData.ItemType == ItemType.Prop && itemData.ExtraId >= 0;
        }

        public static int GetCarryLimit(int itemId)
        {
            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null)
                return 0;

            int fallbackLimit = itemData.MaxStack > 0 ? itemData.MaxStack : 1;
            if (itemData.ItemType != ItemType.Prop || itemData.ExtraId < 0)
                return fallbackLimit;

            PropData propData = DataComponent.Instance.Get<PropData>(itemData.ExtraId);
            return propData != null && propData.CarryLimit > 0
                ? propData.CarryLimit
                : fallbackLimit;
        }

        public static int GetItemCount(CharacterPropData propData, int itemId)
        {
            if (propData?.Slots == null || itemId < 0)
                return 0;

            int count = 0;
            for (int i = 0; i < propData.Slots.Count; i++)
            {
                CharacterPropSlotData slot = propData.Slots[i];
                if (slot != null && slot.ItemId == itemId && slot.Quantity > 0)
                    count += slot.Quantity;
            }

            return count;
        }

        public static int GetAvailableAddCount(CharacterPropData propData, int itemId)
        {
            if (propData?.Slots == null || !IsPropItem(itemId))
                return 0;

            int carryLimit = GetCarryLimit(itemId);
            if (carryLimit <= 0)
                return 0;

            int currentCount = GetItemCount(propData, itemId);
            int remainingLimit = Mathf.Max(0, carryLimit - currentCount);
            if (remainingLimit <= 0)
                return 0;

            if (FindFirstPropSlot(propData, itemId) >= 0 || FindFirstEmptySlot(propData) >= 0)
                return remainingLimit;

            return 0;
        }

        public static bool CanAddProp(CharacterPropData propData, int itemId, int quantity)
        {
            return quantity > 0 && GetAvailableAddCount(propData, itemId) >= quantity;
        }

        public static int AddProp(CharacterPropData propData, int itemId, int quantity)
        {
            if (quantity <= 0 || propData?.Slots == null || !IsPropItem(itemId))
                return 0;

            int addCount = Mathf.Min(quantity, GetAvailableAddCount(propData, itemId));
            if (addCount <= 0)
                return 0;

            int slotIndex = FindFirstPropSlot(propData, itemId);
            if (slotIndex < 0)
                slotIndex = FindFirstEmptySlot(propData);

            if (slotIndex < 0 || slotIndex >= propData.Slots.Count)
                return 0;

            CharacterPropSlotData slot = propData.Slots[slotIndex] ??= new CharacterPropSlotData();
            if (slot.IsEmpty)
                slot.ItemId = itemId;

            slot.Quantity += addCount;
            return addCount;
        }

        public static int FindFirstPropSlot(CharacterPropData propData, int itemId)
        {
            if (propData?.Slots == null || itemId < 0)
                return -1;

            for (int i = 0; i < propData.Slots.Count; i++)
            {
                CharacterPropSlotData slot = propData.Slots[i];
                if (slot != null && slot.ItemId == itemId && slot.Quantity > 0)
                    return i;
            }

            return -1;
        }

        public static bool TryGetSlot(CharacterPropData propData, int slotIndex, out CharacterPropSlotData slot)
        {
            slot = null;
            if (propData?.Slots == null || slotIndex < 0 || slotIndex >= propData.Slots.Count)
                return false;

            slot = propData.Slots[slotIndex];
            return slot != null && !slot.IsEmpty;
        }

        public static bool TryConsumePropSlot(CharacterPropData propData, int slotIndex, int itemId, int count)
        {
            if (count <= 0 || !TryGetSlot(propData, slotIndex, out CharacterPropSlotData slot))
                return false;

            if (slot.ItemId != itemId || slot.Quantity < count)
                return false;

            slot.Quantity -= count;
            if (slot.Quantity <= 0)
                slot.Clear();

            return true;
        }

        public static bool TryMoveBackpackToPropSlot(
            BackpackData backpackData,
            CharacterPropData propData,
            int backpackSlotIndex,
            int propSlotIndex)
        {
            InventoryUtility.EnsureBackpackSlots(backpackData);
            if (!InventoryUtility.IsValidBackpackSlot(backpackData, backpackSlotIndex) ||
                propData?.Slots == null || propSlotIndex < 0 || propSlotIndex >= propData.Slots.Count)
                return false;

            InventoryItemData source = backpackData.Items[backpackSlotIndex];
            if (source == null || source.IsEmpty || !IsPropItem(source.ItemId))
                return false;

            CharacterPropSlotData target = propData.Slots[propSlotIndex] ??= new CharacterPropSlotData();
            int carryLimit = GetCarryLimit(source.ItemId);
            if (carryLimit <= 0)
                return false;

            int sourceItemCountOutsideTarget = GetItemCount(propData, source.ItemId);
            if (!target.IsEmpty && target.ItemId == source.ItemId)
                sourceItemCountOutsideTarget -= target.Quantity;
            int availableCarryCount = Mathf.Max(0, carryLimit - sourceItemCountOutsideTarget);

            if (target.IsEmpty || target.ItemId == source.ItemId)
            {
                int moveCount = Mathf.Min(source.Quantity, Mathf.Max(0, availableCarryCount - (target.IsEmpty ? 0 : target.Quantity)));
                if (moveCount <= 0)
                    return false;

                if (target.IsEmpty)
                    target.ItemId = source.ItemId;
                target.Quantity += moveCount;
                source.Quantity -= moveCount;
                if (source.Quantity <= 0)
                    source.Clear();
                return true;
            }

            if (source.Quantity > availableCarryCount)
                return false;

            int oldItemId = target.ItemId;
            int oldQuantity = target.Quantity;
            ItemData oldItemData = DataComponent.Instance.Get<ItemData>(oldItemId);
            int oldMaxStack = oldItemData != null && oldItemData.MaxStack > 0 ? oldItemData.MaxStack : 1;
            if (oldQuantity > oldMaxStack)
                return false;

            target.ItemId = source.ItemId;
            target.Quantity = source.Quantity;
            source.ItemId = oldItemId;
            source.Quantity = oldQuantity;
            source.ItemType = oldItemData != null ? oldItemData.ItemType : ItemType.Prop;
            return true;
        }

        public static bool TryMovePropToBackpackSlot(
            BackpackData backpackData,
            CharacterPropData propData,
            int propSlotIndex,
            int backpackSlotIndex)
        {
            InventoryUtility.EnsureBackpackSlots(backpackData);
            if (!InventoryUtility.IsValidBackpackSlot(backpackData, backpackSlotIndex) ||
                !TryGetSlot(propData, propSlotIndex, out CharacterPropSlotData source))
                return false;

            InventoryItemData target = backpackData.Items[backpackSlotIndex];
            ItemData sourceItemData = DataComponent.Instance.Get<ItemData>(source.ItemId);
            int sourceMaxStack = sourceItemData != null && sourceItemData.MaxStack > 0 ? sourceItemData.MaxStack : 1;
            if (target.IsEmpty || target.ItemId == source.ItemId)
            {
                int moveCount = Mathf.Min(source.Quantity, Mathf.Max(0, sourceMaxStack - (target.IsEmpty ? 0 : target.Quantity)));
                if (moveCount <= 0)
                    return false;

                if (target.IsEmpty)
                {
                    target.ItemId = source.ItemId;
                    target.ItemType = sourceItemData != null ? sourceItemData.ItemType : ItemType.Prop;
                }
                target.Quantity += moveCount;
                source.Quantity -= moveCount;
                if (source.Quantity <= 0)
                    source.Clear();
                return true;
            }

            if (!IsPropItem(target.ItemId) || source.Quantity > sourceMaxStack)
                return false;

            int targetCarryLimit = GetCarryLimit(target.ItemId);
            int targetItemCountOutsideSource = GetItemCount(propData, target.ItemId);
            if (source.ItemId == target.ItemId)
                targetItemCountOutsideSource -= source.Quantity;
            if (target.Quantity > Mathf.Max(0, targetCarryLimit - targetItemCountOutsideSource))
                return false;

            int oldItemId = source.ItemId;
            int oldQuantity = source.Quantity;
            source.ItemId = target.ItemId;
            source.Quantity = target.Quantity;
            target.ItemId = oldItemId;
            target.Quantity = oldQuantity;
            target.ItemType = sourceItemData != null ? sourceItemData.ItemType : ItemType.Prop;
            return true;
        }

        private static int FindFirstEmptySlot(CharacterPropData propData)
        {
            if (propData?.Slots == null)
                return -1;

            for (int i = 0; i < propData.Slots.Count; i++)
            {
                CharacterPropSlotData slot = propData.Slots[i];
                if (slot == null || slot.IsEmpty)
                    return i;
            }

            return -1;
        }
    }
}
