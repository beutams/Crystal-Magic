using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ItemPresentationTests
{
    [TestCase(ItemType.SkillStone, "技能石", "Skill Stone", "item.type.skill_stone")]
    [TestCase(ItemType.Prop, "道具", "Item", "item.type.prop")]
    [TestCase(ItemType.MagicStone, "魔法石", "Magic Stone", "item.type.magic_stone")]
    [TestCase(ItemType.Spirit, "精灵", "Spirit", "item.type.spirit")]
    public void TypeLabelPrecedesDescriptionWithoutChangingTheRawDescription(
        ItemType type, string chinese, string english, string key)
    {
        const string descriptionKey = "ui.confirm.save_current.content";
        var item = new ItemData { ItemType = type, DescriptionKey = descriptionKey };
        string description = LocalizationComponent.Resolve(descriptionKey);
        Assert.That(item.TypeName, Is.EqualTo(chinese));
        Assert.That(item.Description, Is.EqualTo(description));
        Assert.That(item.DescriptionWithType, Is.EqualTo($"【{chinese}】{description}"));
        Assert.That(item.DescriptionWithType, Is.EqualTo(item.DescriptionWithType), "Formatting must not accumulate labels.");

        var rows = (JArray)JObject.Parse(File.ReadAllText("Assets/Res/Data/LocalizationDataTable.json"))["Rows"];
        JToken translation = rows.Single(row => (string)row["Key"] == key);
        Assert.That((string)translation["English"], Is.EqualTo(english));
        item.DescriptionKey = null;
        Assert.That(item.DescriptionWithType, Is.EqualTo($"【{chinese}】"));
    }

    [TestCase(ItemType.None)]
    [TestCase((ItemType)999)]
    public void UnknownTypesDoNotAddAnEmptyOrMisleadingLabel(ItemType type)
    {
        var item = new ItemData { ItemType = type, DescriptionKey = "ui.confirm.save_current.content" };
        Assert.That(item.TypeName, Is.Empty);
        Assert.That(item.DescriptionWithType, Is.EqualTo(item.Description));
        item.DescriptionKey = null;
        Assert.That(item.DescriptionWithType, Is.Empty);
    }

    [Test]
    public void DisplayPropertiesAreNotSerializedIntoItemData()
    {
        var item = new ItemData { ItemType = ItemType.MagicStone };
        var json = JObject.Parse(JsonConvert.SerializeObject(item));
        Assert.That(json.Property(nameof(ItemData.TypeName)), Is.Null);
        Assert.That(json.Property(nameof(ItemData.DescriptionWithType)), Is.Null);
        Assert.That(json[nameof(ItemData.ItemType)].Value<int>(), Is.EqualTo((int)ItemType.MagicStone));
    }

    [Test]
    public void EquipmentPlaceholderUsesTheApprovedNativePixelRunestone()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/UI/CharacterUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        var ui = new CharacterUIData();
        ui.Bind(prefab.transform);
        Sprite sprite = ui.Equip_MagicStoneBorder_Default.Image.sprite;
        Assert.That(sprite, Is.Not.Null);
        const string path = "Assets/Res/Sprites/UISprites/BookV1/Content/Icons/MagicRunestone.png";
        Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(path));
        Assert.That(sprite, Is.SameAs(ui.Buttons_Skill_Icon.Image.sprite));
        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
    }
}
