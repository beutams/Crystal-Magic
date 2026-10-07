using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public sealed class EquipmentItemConfigurationTests
{
    private const string IconRoot = "Assets/Res/Sprites/ItemIcon/Equipment/";

    [Serializable]
    private sealed class Table<T> { public List<T> Rows = new(); }

    private static List<T> ReadRows<T>(string tableName) =>
        JsonUtility.FromJson<Table<T>>(File.ReadAllText("Assets/Res/Data/" + tableName + ".json")).Rows;

    private static List<ItemData> EquipmentItems() => ReadRows<ItemData>("ItemDataTable")
        .Where(item => item.ItemType == ItemType.MagicStone || item.ItemType == ItemType.Spirit).ToList();

    private static EquipData EquipmentFor(int itemId)
    {
        ItemData item = EquipmentItems().Single(row => row.Id == itemId);
        return ReadRows<EquipData>("EquipDataTable").Single(row => row.Id == item.ExtraId);
    }

    [Test]
    public void EveryEquipmentItemHasADistinctLoadablePixelSpriteAndValidProperties()
    {
        List<ItemData> items = EquipmentItems();
        var paths = new HashSet<string>();
        foreach (ItemData item in items)
        {
            Assert.That(item.IconPath, Does.StartWith(IconRoot), item.NameKey);
            Assert.That(paths.Add(item.IconPath), Is.True, item.NameKey);
            string[] path = item.IconPath.Split('|');
            Assert.That(path, Has.Length.EqualTo(2));
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path[0]).OfType<Sprite>().Single(icon => icon.name == path[1]);
            Assert.That(sprite, Is.Not.Null, item.IconPath);
            Assert.That(sprite.texture.width, Is.EqualTo(64), item.IconPath);
            Assert.That(sprite.texture.height, Is.EqualTo(64), item.IconPath);
            Assert.That(sprite.rect.width, Is.LessThan(sprite.texture.width));
            Assert.That(sprite.rect.width, Is.EqualTo(sprite.rect.height).Within(0.001f));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path[0]);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(item.Rarity, Is.EqualTo(1));
            Assert.That(item.MaxStack, Is.EqualTo(1));
            Assert.That(EquipmentFor(item.Id).Properties, Is.Not.Empty, item.NameKey);
        }
        Assert.That(items, Has.Count.EqualTo(11));
    }

    [TestCase(1, PropertyModifierChannel.FirePower, 0.3f)]
    [TestCase(5, PropertyModifierChannel.WaterPower, 0.3f)]
    [TestCase(6, PropertyModifierChannel.LightningPower, 0.3f)]
    [TestCase(7, PropertyModifierChannel.AttackPower, 2f)]
    [TestCase(8, PropertyModifierChannel.ChantSpeed, 25f)]
    [TestCase(9, PropertyModifierChannel.MoveSpeed, 0.5f)]
    [TestCase(10, PropertyModifierChannel.MaxHealth, 50f)]
    [TestCase(10, PropertyModifierChannel.Defense, 3f)]
    [TestCase(11, PropertyModifierChannel.MaxMp, 20f)]
    [TestCase(12, PropertyModifierChannel.HealthRegen, 1f)]
    [TestCase(12, PropertyModifierChannel.MpRegen, 2f)]
    [TestCase(32, PropertyModifierChannel.WaterPower, 0.2f)]
    [TestCase(32, PropertyModifierChannel.FirePower, 0.2f)]
    [TestCase(32, PropertyModifierChannel.LightningPower, 0.2f)]
    [TestCase(32, PropertyModifierChannel.WindPower, 0.2f)]
    public void FormalItemsUseTheIntendedStartingBonuses(int id, PropertyModifierChannel channel, float value)
    {
        EquipPropertyEntry entry = EquipmentFor(id).Properties.Single(property => property.Channel == channel);
        Assert.That(entry.BaseBonus, Is.EqualTo(value).Within(0.0001f));
        Assert.That(EquipmentItems().Single(item => item.Id == id).DescriptionKey, Is.Not.Empty);
    }

    [Test]
    public void TestStoneKeepsItsDebugValuesAndDoesNotShareAFormalConfiguration()
    {
        EquipData test = EquipmentFor(31);
        Assert.That(test.Id, Is.EqualTo(1));
        Assert.That(test.Properties.Select(property => property.Channel), Is.EquivalentTo(new[]
        {
            PropertyModifierChannel.MaxHealth, PropertyModifierChannel.MaxMp,
            PropertyModifierChannel.HealthRegen, PropertyModifierChannel.MpRegen,
        }));
        Assert.That(test.Properties.All(property => property.BaseBonus == 1000f), Is.True);
        foreach (ItemData item in EquipmentItems().Where(item => item.Id != 31))
            Assert.That(item.ExtraId, Is.Not.EqualTo(test.Id));
    }

    private static EquipmentPropertyData BuildProperties(params int[] itemIds)
    {
        // Exercise the runtime channel mapping without creating or replacing any scene singleton.
        MethodInfo add = typeof(EquipmentUtility).GetMethod("AddBonus", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(add, Is.Not.Null);
        EquipmentPropertyData properties = default;
        foreach (int itemId in itemIds)
        foreach (EquipPropertyEntry entry in EquipmentFor(itemId).Properties)
        {
            object[] args = { properties, entry.Channel, entry.BaseBonus };
            add.Invoke(null, args);
            properties = (EquipmentPropertyData)args[0];
        }
        return properties;
    }

    [Test]
    public void BonusesApplyIdempotentlyToUnitStatsAndUnequippingClearsThem()
    {
        using var world = new World("Equipment configuration test");
        EntityManager manager = world.EntityManager;
        Entity unit = manager.CreateEntity(typeof(UnitMoveComponent), typeof(UnitVitalityComponent),
            typeof(UnitManaComponent), typeof(UnitAttackComponent), typeof(UnitElementComponent));
        manager.SetComponentData(unit, new UnitMoveComponent { BaseMoveSpeed = 6f });
        manager.SetComponentData(unit, new UnitVitalityComponent { BaseMaxHealth = 100f, BaseHealthRegenPerSecond = 5f });
        manager.SetComponentData(unit, new UnitManaComponent { BaseMaxMp = 100f, BaseMpRegenPerSecond = 10f });
        manager.SetComponentData(unit, new UnitAttackComponent { BaseAttackPower = 10f });

        EquipmentPropertyData properties = BuildProperties(1, 7, 8, 9, 10);
        UnitModifierUtility.ApplyEquipmentProperties(manager, unit, in properties);
        UnitModifierUtility.ApplyEquipmentProperties(manager, unit, in properties);
        Assert.That(UnitModifierResolver.GetMoveSpeed(manager, unit), Is.EqualTo(6.5f).Within(0.0001f));
        Assert.That(UnitModifierResolver.GetMaxHealth(manager, unit), Is.EqualTo(150f));
        Assert.That(UnitModifierResolver.GetDefense(manager, unit), Is.EqualTo(3f));
        Assert.That(UnitModifierResolver.GetAttackPower(manager, unit), Is.EqualTo(12f));
        float chant = UnitModifierResolver.GetChantSpeedBonus(manager, unit);
        Assert.That(chant, Is.EqualTo(25f));
        Assert.That(UnitAttackComponent.GetDurationMultiplier(chant), Is.EqualTo(1f / 1.25f).Within(0.0001f));
        Assert.That(manager.GetComponentData<UnitElementComponent>(unit).FirePower, Is.EqualTo(0.3f));

        properties = BuildProperties(32);
        UnitModifierUtility.ApplyEquipmentProperties(manager, unit, in properties);
        UnitModifierUtility.ApplyEquipmentProperties(manager, unit, in properties);
        UnitElementComponent allElements = manager.GetComponentData<UnitElementComponent>(unit);
        Assert.That(allElements.WaterPower, Is.EqualTo(0.2f));
        Assert.That(allElements.FirePower, Is.EqualTo(0.2f));
        Assert.That(allElements.LightningPower, Is.EqualTo(0.2f));
        Assert.That(allElements.WindPower, Is.EqualTo(0.2f));

        properties = BuildProperties(5, 11, 12, 12, 11);
        UnitModifierUtility.ApplyEquipmentProperties(manager, unit, in properties);
        Assert.That(UnitModifierResolver.GetMaxMp(manager, unit), Is.EqualTo(140f));
        Assert.That(UnitModifierResolver.GetHealthRegen(manager, unit), Is.EqualTo(7f));
        Assert.That(UnitModifierResolver.GetMpRegen(manager, unit), Is.EqualTo(14f));
        Assert.That(manager.GetComponentData<UnitElementComponent>(unit).WaterPower, Is.EqualTo(0.3f));
        Assert.That(manager.GetComponentData<UnitElementComponent>(unit).FirePower, Is.Zero);
        Assert.That(manager.GetComponentData<UnitElementComponent>(unit).LightningPower, Is.Zero);
        Assert.That(manager.GetComponentData<UnitElementComponent>(unit).WindPower, Is.Zero);
        Assert.That(UnitModifierResolver.GetAttackPower(manager, unit), Is.EqualTo(10f));

        properties = default;
        UnitModifierUtility.ApplyEquipmentProperties(manager, unit, in properties);
        Assert.That(UnitModifierResolver.GetMaxMp(manager, unit), Is.EqualTo(100f));
        Assert.That(UnitModifierResolver.GetHealthRegen(manager, unit), Is.EqualTo(5f));
        Assert.That(UnitModifierResolver.GetMpRegen(manager, unit), Is.EqualTo(10f));
        Assert.That(manager.GetComponentData<UnitElementComponent>(unit).WaterPower, Is.Zero);
    }
}
