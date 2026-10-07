using NUnit.Framework;
using Unity.Entities;

public sealed class SkillProjectileRepeatHitTests
{
    [Test]
    public void DefaultProjectileNeverRehitsAnAlreadyHitTarget()
    {
        using World world = new("Projectile once-only test");
        Entity holder = world.EntityManager.CreateEntity();
        Entity target = world.EntityManager.CreateEntity();
        var hits = world.EntityManager.AddBuffer<SkillProjectileHitEntityElement>(holder);
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, target, false), Is.False);
        SkillProjectileHitHistory.Record(ref hits, target, 0f);
        SkillProjectileHitHistory.Advance(ref hits, 10f);
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, target, false), Is.True);
    }

    [Test]
    public void RepeatedHitWaitsForItsOwnCooldownAndDoesNotDuplicateHistory()
    {
        using World world = new("Projectile repeat test");
        Entity holder = world.EntityManager.CreateEntity();
        Entity target = world.EntityManager.CreateEntity();
        Entity other = world.EntityManager.CreateEntity();
        var hits = world.EntityManager.AddBuffer<SkillProjectileHitEntityElement>(holder);
        SkillProjectileHitHistory.Record(ref hits, target, .5f);
        SkillProjectileHitHistory.Advance(ref hits, .25f);
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, target, true), Is.True);
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, other, true), Is.False);
        SkillProjectileHitHistory.Record(ref hits, other, .5f);
        SkillProjectileHitHistory.Advance(ref hits, .25f);
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, target, true), Is.False);
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, other, true), Is.True);
        SkillProjectileHitHistory.Record(ref hits, target, .5f);
        Assert.That(hits.Length, Is.EqualTo(2));
        Assert.That(SkillProjectileHitHistory.Blocks(in hits, target, true), Is.True);
    }

    [Test]
    public void LongFrameClampsCooldownInsteadOfBuildingAHitBacklog()
    {
        using World world = new("Projectile long frame test");
        Entity holder = world.EntityManager.CreateEntity();
        Entity target = world.EntityManager.CreateEntity();
        var hits = world.EntityManager.AddBuffer<SkillProjectileHitEntityElement>(holder);
        SkillProjectileHitHistory.Record(ref hits, target, .5f);
        SkillProjectileHitHistory.Advance(ref hits, 2f);
        Assert.That(hits[0].CooldownRemaining, Is.Zero);
        SkillProjectileHitHistory.Record(ref hits, target, .5f);
        Assert.That(hits[0].CooldownRemaining, Is.EqualTo(.5f));
        Assert.That(hits.Length, Is.EqualTo(1));
    }
}
