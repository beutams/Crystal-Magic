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

        public static bool TryBindShortcut(CharacterPropData propData, int shortcutIndex, int propSlotIndex)
        {
            if (propData?.ShortcutSlotIndexes == null || shortcutIndex < 0 || shortcutIndex >= propData.ShortcutSlotIndexes.Length)
                return false;

            if (propSlotIndex < -1 || propData.Slots == null || propSlotIndex >= propData.Slots.Count)
                return false;

            propData.ShortcutSlotIndexes[shortcutIndex] = propSlotIndex;
            return true;
        }

        public static bool TryGetShortcutPropSlot(CharacterPropData propData, int shortcutIndex, out int propSlotIndex)
        {
            propSlotIndex = -1;
            if (propData?.ShortcutSlotIndexes == null || shortcutIndex < 0 || shortcutIndex >= propData.ShortcutSlotIndexes.Length)
                return false;

            propSlotIndex = propData.ShortcutSlotIndexes[shortcutIndex];
            return propSlotIndex >= 0 && propData.Slots != null && propSlotIndex < propData.Slots.Count;
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
