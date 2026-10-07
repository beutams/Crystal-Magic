using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using NUnit.Framework;

public sealed class InventorySortTests
{
    private static InventoryItemData Item(int id, int quantity, ItemType type = ItemType.Prop) =>
        new() { ItemId = id, Quantity = quantity, ItemType = type };

    private static ItemData Definition(int id) => id == 999 ? null : new ItemData
    {
        ItemType = id < 10 ? ItemType.SkillStone : ItemType.Prop,
        Rarity = id == 2 ? 3 : 0,
        MaxStack = id == 20 ? 1 : 10,
    };

    private static string Totals(IEnumerable<InventoryItemData> items) => string.Join(";", items
        .Where(i => i != null && !i.IsEmpty).GroupBy(i => (i.ItemId, i.ItemType))
        .OrderBy(g => g.Key.ItemId).ThenBy(g => g.Key.ItemType)
        .Select(g => $"{g.Key}:{g.Sum(i => (long)i.Quantity)}"));

    [Test]
    public void SortMergesPartialStacksKeepsSlotCountAndPlacesEmptySlotsLast()
    {
        var items = new List<InventoryItemData> { Item(10, 7), null, Item(10, 6), Item(10, 5), new() };
        string before = Totals(items);
        Assert.That(InventoryUtility.SortItems(items, true, Definition), Is.True);
        Assert.That(items.Count, Is.EqualTo(5));
        Assert.That(items.Select(i => i.Quantity), Is.EqualTo(new[] { 10, 8, 0, 0, 0 }));
        Assert.That(Totals(items), Is.EqualTo(before));
        Assert.That(InventoryUtility.SortItems(items, true, Definition), Is.False);
    }

    [Test]
    public void TypeThenRarityThenItemIdDetermineOrder()
    {
        var items = new List<InventoryItemData> { Item(20, 1), Item(3, 1), Item(2, 1), Item(1, 1), Item(10, 1) };
        InventoryUtility.SortItems(items, false, Definition);
        Assert.That(items.Select(i => i.ItemId), Is.EqualTo(new[] { 2, 1, 3, 10, 20 }));
    }

    [Test]
    public void WarehouseCompactsWithoutChangingMoneyCapacityOrQuantities()
    {
        var stash = new StashData { Capacity = 100, Money = 345, Items = new() { null, Item(10, 4), Item(10, 6), new() } };
        string before = Totals(stash.Items);
        InventoryUtility.SortItems(stash.Items, false, Definition);
        Assert.That(stash.Items.Count, Is.EqualTo(1));
        Assert.That(stash.Items[0].Quantity, Is.EqualTo(10));
        Assert.That(stash.Capacity, Is.EqualTo(100));
        Assert.That(stash.Money, Is.EqualTo(345));
        Assert.That(Totals(stash.Items), Is.EqualTo(before));
    }

    [Test]
    public void UnstackableUnknownAndOversizedLegacyStacksNeverLoseItems()
    {
        var items = new List<InventoryItemData> { Item(999, int.MaxValue), Item(20, 1), Item(20, 1), Item(10, 24), Item(10, 2), Item(999, 5) };
        string before = Totals(items);
        InventoryUtility.SortItems(items, true, Definition);
        Assert.That(items.Count, Is.EqualTo(6));
        Assert.That(items.Count(i => i.ItemId == 20), Is.EqualTo(2));
        Assert.That(items.Count(i => i.ItemId == 999), Is.EqualTo(2));
        Assert.That(Totals(items), Is.EqualTo(before));
        Assert.That(InventoryUtility.SortItems(items, true, Definition), Is.False);
    }

    [Test]
    public void EmptyInventoriesAreSafeAndRepeatedSortIsANoOp()
    {
        Assert.That(InventoryUtility.SortItems(null, true, Definition), Is.False);
        var items = new List<InventoryItemData> { null, new(), new() };
        InventoryUtility.SortItems(items, true, Definition);
        Assert.That(items.Count, Is.EqualTo(3));
        Assert.That(items.All(i => i != null && i.IsEmpty), Is.True);
        Assert.That(InventoryUtility.SortItems(items, true, Definition), Is.False);
        InventoryUtility.SortItems(items, false, Definition);
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void FiveHundredRandomInventoriesPreserveTotalsCapacityAndIdempotence()
    {
        var random = new Random(2347);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            int count = random.Next(1, 120);
            var items = new List<InventoryItemData>();
            for (int i = 0; i < count; i++)
                items.Add(random.Next(4) == 0 ? null : Item(random.Next(1, 25), random.Next(1, 11)));
            string before = Totals(items);
            bool keepEmpty = iteration % 2 == 0;
            InventoryUtility.SortItems(items, keepEmpty, Definition);
            Assert.That(Totals(items), Is.EqualTo(before), $"iteration {iteration}");
            Assert.That(items.Count, keepEmpty ? Is.EqualTo(count) : Is.LessThanOrEqualTo(count));
            Assert.That(InventoryUtility.SortItems(items, keepEmpty, Definition), Is.False, $"iteration {iteration}");
        }
    }
}
