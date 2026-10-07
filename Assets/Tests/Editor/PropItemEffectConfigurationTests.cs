using System.IO;
using System.Linq;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class PropItemEffectConfigurationTests
{
    private static JArray Rows(string name) =>
        (JArray)JObject.Parse(File.ReadAllText($"Assets/Res/Data/{name}DataTable.json"))["Rows"];

    private static PropData Prop(int id) => Rows("Prop").Single(row => (int)row["Id"] == id)
        .ToObject<PropData>(JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
        }));

    [TestCase(2, 1, "恢复50HP")]
    [TestCase(3, 2, "恢复50MP")]
    public void PotionEffectsMatchTheirVisibleItemDescriptions(int itemId, int propId, string description)
    {
        ItemData item = Rows("Item").Single(row => (int)row["Id"] == itemId).ToObject<ItemData>();
        PropData prop = Prop(propId);
        Assert.That(item.ItemType, Is.EqualTo(ItemType.Prop));
        Assert.That(item.ExtraId, Is.EqualTo(prop.Id));
        Assert.That(prop.NameKey, Is.EqualTo(item.NameKey));
        Assert.That(item.DescriptionKey, Is.EqualTo(description));
        Assert.That(prop.DescriptionKey, Is.EqualTo(item.DescriptionKey));
        Assert.That(prop.TargetType, Is.EqualTo(PropTargetType.Self));
        Assert.That(prop.CarryLimit, Is.EqualTo(10));
        Assert.That(prop.EffectChain, Has.Length.EqualTo(1));
        EffectData effect = prop.EffectChain.Single();
        Assert.That(effect.Conditions, Is.Empty);
        Assert.That(EffectExecutionTargetUtility.CanExecute(effect, GameWorldRole.Standalone), Is.True);
        Assert.That(EffectExecutionTargetUtility.CanExecute(effect, GameWorldRole.Server), Is.True);
        Assert.That(EffectExecutionTargetUtility.CanExecute(effect, GameWorldRole.Client), Is.False);
        if (propId == 1)
        {
            Assert.That(effect, Is.TypeOf<HealEffectData>());
            var heal = (HealEffectData)effect;
            Assert.That(heal.HealCoefficient, Is.Zero, "Potions must not scale with attack power.");
            Assert.That(heal.FlatHealBonus, Is.EqualTo(50f));
        }
        else
        {
            Assert.That(effect, Is.TypeOf<RestoreManaEffectData>());
            var mana = (RestoreManaEffectData)effect;
            Assert.That(mana.ManaRestoreCoefficient, Is.Zero, "Potions must not scale with attack power.");
            Assert.That(mana.FlatManaRestoreBonus, Is.EqualTo(50f));
        }
    }

    [Test]
    public void EveryUsablePropItemHasAnExecutableEffectChain()
    {
        ItemData[] items = Rows("Item").Select(row => row.ToObject<ItemData>())
            .Where(item => item.ItemType == ItemType.Prop).ToArray();
        Assert.That(items, Is.Not.Empty);
        foreach (ItemData item in items)
        {
            PropData prop = Prop(item.ExtraId);
            Assert.That(prop.EffectChain, Is.Not.Empty, item.NameKey);
            Assert.That(prop.EffectChain.All(effect => effect != null &&
                EffectExecutionTargetUtility.CanExecute(effect, GameWorldRole.Standalone) &&
                EffectExecutionTargetUtility.CanExecute(effect, GameWorldRole.Server)), Is.True, item.NameKey);
        }
    }
}
