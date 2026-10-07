using CrystalMagic.Core;
using NUnit.Framework;
using Unity.Entities;

public sealed class SkillChainLimitTests
{
    private static CharacterData WithSlots(int count)
    {
        var data = new CharacterData();
        for (int i = 0; i < count; i++)
            data.Skills.Chains[0].Slots.Add(new SkillChainSlotData { SkillStoneItemId = i });
        return data;
    }

    [Test]
    public void TenIsFullNineCanStillAcceptASkill()
    {
        Assert.That(SkillChainData.MaxLength, Is.EqualTo(10));
        Assert.That(WithSlots(9).Skills.Chains[0].IsFull, Is.False);
        Assert.That(WithSlots(10).Skills.Chains[0].IsFull, Is.True);
        Assert.That(WithSlots(11).Skills.Chains[0].IsFull, Is.True);
    }

    [Test]
    public void AuthoritativeEditsAcceptTenAndRejectElevenWithoutMutatingData()
    {
        var character = new PlayerCharacterComponent();
        Assert.That(character.TryEdit(0, WithSlots(10)), Is.True);
        Assert.That(character.TryEdit(1, WithSlots(11)), Is.False);
        Assert.That(character.Revision, Is.EqualTo(1));
        Assert.That(character.Data.Skills.Chains[0].Slots.Count, Is.EqualTo(10));
        Assert.That(character.TryEdit(1, WithSlots(9)), Is.True);
    }

    [Test]
    public void LegacyOversizedChainsCanBeRecoveredButCannotGrow()
    {
        var character = new PlayerCharacterComponent { Data = WithSlots(12) };
        Assert.That(character.TryEdit(0, WithSlots(12)), Is.True);
        Assert.That(character.TryEdit(1, WithSlots(13)), Is.False);
        Assert.That(character.TryEdit(1, WithSlots(11)), Is.True);
        Assert.That(character.TryEdit(2, WithSlots(10)), Is.True);
    }

    [Test]
    public void RuntimeUsesOnlyTenWhileLeavingLegacyStonesAvailableToRecover()
    {
        using World world = new("Skill chain limit test");
        var manager = world.EntityManager;
        Entity player = manager.CreateEntity();
        var character = new PlayerCharacterComponent { Data = WithSlots(12) };
        manager.AddComponentObject(player, character);
        PlayerSkillChainUtility.Rebuild(manager, player);
        Assert.That(manager.GetBuffer<PlayerSkillChainElement>(player)[0].SlotCount, Is.EqualTo(10));
        Assert.That(manager.GetBuffer<PlayerSkillChainSlotElement>(player).Length, Is.EqualTo(10));
        Assert.That(character.Data.Skills.Chains[0].Slots.Count, Is.EqualTo(12));
    }

    [Test]
    public void BattleHudMatchesTheTenActiveRuntimeSlots()
    {
        var data = WithSlots(12);
        var build = typeof(CrystalMagic.UI.BattleUIModel).GetMethod("BuildSkillItems",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var items = (System.Collections.IList)build.Invoke(null, new object[] { data.Skills, 0, 0, 9 });
        Assert.That(items.Count, Is.EqualTo(SkillChainData.MaxLength));
        Assert.That(data.Skills.Chains[0].Slots.Count, Is.EqualTo(12));
    }
}
