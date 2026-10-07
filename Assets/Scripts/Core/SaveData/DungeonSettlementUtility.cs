using System;
using System.Collections.Generic;
using Unity.Entities;

namespace CrystalMagic.Core
{
    /// <summary>Settlement is a report only: it never grants, consumes or moves inventory.</summary>
    public static class DungeonSettlementUtility
    {
        public static DungeonSettlementResult CreateResult(
            DungeonSettlementOutcome outcome, DungeonRunData run, CharacterData character)
        {
            bool success = outcome == DungeonSettlementOutcome.Escaped;
            return new DungeonSettlementResult
            {
                Outcome = outcome,
                ReachedFloor = Math.Max(1, run?.CurrentFloor ?? 1),
                Items = success ? CopyItems(run?.AcquiredItems) : CaptureCarriedItems(character),
                Money = Math.Max(0L, success ? run?.AcquiredMoney ?? 0L : character?.Money ?? 0L),
                HasCompleteHistory = !success || run?.HasAcquisitionHistory == true,
            };
        }

        public static List<InventoryItemData> CaptureCarriedItems(CharacterData character)
        {
            List<InventoryItemData> items = CopyItems(character?.Backpack?.Items);
            EquipmentData equipment = character?.Equipment;
            if (equipment != null)
            {
                Add(items, equipment.MagicStoneId, 1);
                if (equipment.SpiritSlots != null)
                    foreach (int itemId in equipment.SpiritSlots)
                        Add(items, itemId, 1);
            }

            if (character?.Props?.Slots != null)
                foreach (CharacterPropSlotData slot in character.Props.Slots)
                    if (slot != null)
                        Add(items, slot.ItemId, slot.Quantity);

            // Equipped skill stones are physical items; addition IDs are not item IDs.
            if (character?.Skills?.Chains != null)
                foreach (SkillChainData chain in character.Skills.Chains)
                    if (chain?.Slots != null)
                        foreach (SkillChainSlotData slot in chain.Slots)
                            if (slot != null)
                                Add(items, slot.SkillStoneItemId, 1);
            return items;
        }

        public static void RecordItemAcquired(EntityManager manager, int itemId, int quantity)
        {
            if (GameWorldContextUtility.GetSceneMode(manager) != GameSceneMode.Dungeon ||
                !GameRuntimeStateUtility.TryGetComponentObject(manager, out DungeonRunComponent run))
                return;

            run.AcquiredItems ??= new List<InventoryItemData>();
            Add(run.AcquiredItems, itemId, quantity);
        }

        public static void RecordMoneyAcquired(EntityManager manager, int quantity)
        {
            if (quantity <= 0 || GameWorldContextUtility.GetSceneMode(manager) != GameSceneMode.Dungeon ||
                !GameRuntimeStateUtility.TryGetComponentObject(manager, out DungeonRunComponent run))
                return;

            run.AcquiredMoney = run.AcquiredMoney > long.MaxValue - quantity
                ? long.MaxValue : run.AcquiredMoney + quantity;
        }

        private static List<InventoryItemData> CopyItems(IEnumerable<InventoryItemData> source)
        {
            List<InventoryItemData> result = new();
            if (source != null)
                foreach (InventoryItemData item in source)
                    if (item != null)
                        Add(result, item.ItemId, item.Quantity);
            return result;
        }

        private static void Add(List<InventoryItemData> items, int itemId, int quantity)
        {
            if (itemId < 0 || quantity <= 0)
                return;
            foreach (InventoryItemData item in items)
            {
                if (item.ItemId != itemId)
                    continue;
                item.Quantity = (int)Math.Min(int.MaxValue, (long)item.Quantity + quantity);
                return;
            }
            items.Add(new InventoryItemData { ItemId = itemId, Quantity = quantity });
        }
    }
}
