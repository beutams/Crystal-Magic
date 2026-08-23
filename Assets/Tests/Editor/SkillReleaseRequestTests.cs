using NUnit.Framework;
using CrystalMagic.Game.Data;
using Unity.Entities;

public sealed class SkillReleaseRequestTests
{
    [Test]
    public void Create_CopiesSuppliedExtraModifiers()
    {
        using World world = new("SkillReleaseRequestTests");
        EntityManager entityManager = world.EntityManager;
        Entity caster = entityManager.CreateEntity();
        SkillModifierSet supplied = new();
        supplied.Add(new SkillModifierEntry
        {
            Channel = SkillModifierChannel.Damage,
            Bonus = 10f,
        });

        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(entityManager, caster, 7, supplied);
        supplied.Add(new SkillModifierEntry
        {
            Channel = SkillModifierChannel.Damage,
            Bonus = 5f,
        });

        Assert.That(request.ExtraModifiers.GetBonus(SkillModifierChannel.Damage), Is.EqualTo(10f));
    }
}
