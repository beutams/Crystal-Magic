using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;

public sealed class UnitDamageAggroTests
{
    [Test]
    public void MemoryExpiresAtDeadlineAndNullTargetNeverActivates()
    {
        var memory = new UnitPerceptionComponent
        {
            DamageTarget = new Entity { Index = 1, Version = 1 },
            DamageTargetUntil = 10 + UnitDamageAggroUtility.DurationSeconds,
        };
        Assert.That(UnitDamageAggroUtility.DurationSeconds, Is.EqualTo(8));
        Assert.That(memory.HasDamageTarget(17.999), Is.True);
        Assert.That(memory.HasDamageTarget(18), Is.False);
        memory.DamageTarget = Entity.Null;
        Assert.That(memory.HasDamageTarget(10), Is.False);
    }

    [Test]
    public void DamageOutsideSightTracksMovingAttackerThenExpires()
    {
        using var f = new Fixture();
        Entity monster = f.Monster();
        f.Damage(monster);
        f.Position(f.Player, 40);
        f.Tick(1);
        Assert.That(UnitPerceptionQueryUtility.TryGetNearestByFaction(f.Manager, monster,
            UnitFactionType.Player, out Entity target, out float distance), Is.True);
        Assert.That(target, Is.EqualTo(f.Player));
        Assert.That(distance, Is.EqualTo(40), "Memory must not fake a close-range attack distance.");
        Assert.That(f.Memory(monster).DamageTargetUntil, Is.EqualTo(8), "Perception must not refresh damage memory.");
        f.Tick(8);
        Assert.That(f.Memory(monster).DamageTarget, Is.EqualTo(Entity.Null));
        Assert.That(f.Manager.GetBuffer<UnitPerceptionUnitElement>(monster).Length, Is.Zero);
    }

    [Test]
    public void RepeatedDamageRefreshesVictimAndSquadButDoesNotPullOtherSquads()
    {
        using var f = new Fixture();
        Entity owner = f.Owner(), otherOwner = f.Owner();
        Entity victim = f.Monster(owner), mate = f.Monster(owner), other = f.Monster(otherOwner);
        Entity dead = f.Monster(owner);
        f.Manager.SetComponentEnabled<UnitDeathComponent>(dead, true);
        // Stale membership must not alert a unit that has moved to another owner.
        f.Manager.GetBuffer<UnitVariableConsumerElement>(owner).Add(new UnitVariableConsumerElement { Value = other });
        f.Damage(victim);
        Assert.That(f.Memory(mate).DamageTarget, Is.EqualTo(f.Player));
        Assert.That(f.Memory(dead).DamageTarget, Is.EqualTo(Entity.Null));
        Assert.That(f.Memory(other).DamageTarget, Is.EqualTo(Entity.Null));
        f.Time(6);
        f.Damage(victim);
        Assert.That(f.Memory(victim).DamageTargetUntil, Is.EqualTo(14));
        Assert.That(f.Memory(mate).DamageTargetUntil, Is.EqualTo(14));
    }

    [Test]
    public void PatrolDoesNotAlertItsDistantHomeGuards()
    {
        using var f = new Fixture();
        Entity owner = f.Owner();
        Entity guard = f.Monster(owner), patrol = f.Monster(owner, true), mate = f.Monster(owner, true);
        f.Damage(patrol);
        Assert.That(f.Memory(mate).DamageTarget, Is.EqualTo(f.Player));
        Assert.That(f.Memory(guard).DamageTarget, Is.EqualTo(Entity.Null));
    }

    [Test]
    public void LethalHitStillAlertsSurvivingSquad()
    {
        using var f = new Fixture();
        Entity owner = f.Owner(), victim = f.Monster(owner), mate = f.Monster(owner);
        f.Damage(victim, 200);
        Assert.That(f.Manager.IsComponentEnabled<UnitDeathComponent>(victim), Is.True);
        Assert.That(f.Memory(victim).DamageTarget, Is.EqualTo(Entity.Null));
        Assert.That(f.Memory(mate).DamageTarget, Is.EqualTo(f.Player));
    }

    [Test]
    public void ZeroDamageAndNonPlayerDamageDoNotStartAggro()
    {
        using var f = new Fixture();
        Entity victim = f.Monster(), otherMonster = f.Monster();
        f.Damage(victim, 0);
        Assert.That(f.Memory(victim).DamageTarget, Is.EqualTo(Entity.Null));
        new DamageEffect(new DamageEffectData { FlatDamageBonus = 1, DamageCoefficient = 0 })
            .Execute(f.Context(victim, otherMonster));
        Assert.That(f.Memory(victim).DamageTarget, Is.EqualTo(Entity.Null));
    }

    [Test]
    public void BuffDamageAlsoStartsMemoryWithoutReactiveHook()
    {
        using var f = new Fixture();
        Entity victim = f.Monster();
        new BuffDamageEffect(new BuffDamageEffectData { FlatDamageBonus = 1, DamageCoefficient = 0 })
            .Execute(f.Context(victim, f.Player));
        Assert.That(f.Memory(victim).DamageTarget, Is.EqualTo(f.Player));
    }

    [TestCase("dead")]
    [TestCase("destroyed")]
    [TestCase("spectator")]
    [TestCase("disabled")]
    public void UnavailableAttackerClearsMemory(string reason)
    {
        using var f = new Fixture();
        Entity victim = f.Monster();
        f.Damage(victim);
        switch (reason)
        {
            case "dead": f.Manager.SetComponentEnabled<UnitDeathComponent>(f.Player, true); break;
            case "destroyed": f.Manager.DestroyEntity(f.Player); break;
            case "spectator": f.Manager.AddComponent<BattleSpectatorComponent>(f.Player); break;
            case "disabled": f.Manager.SetEnabled(f.Player, false); break;
        }
        f.Tick(1);
        Assert.That(f.Memory(victim).DamageTarget, Is.EqualTo(Entity.Null));
        Assert.That(f.Manager.GetBuffer<UnitPerceptionUnitElement>(victim).Length, Is.Zero);
    }

    [Test]
    public void AttackerHasPriorityWithoutDuplicatePerceptionAndNormalSightRemainsAfterExpiry()
    {
        using var f = new Fixture();
        Entity victim = f.Monster();
        f.Position(f.Player, 2);
        Entity closerPlayer = f.PlayerAt(1);
        f.Damage(victim);
        f.Tick(1);
        Assert.That(UnitPerceptionQueryUtility.GetCountByFaction(f.Manager, victim, UnitFactionType.Player), Is.EqualTo(2));
        UnitPerceptionQueryUtility.TryGetNearestByFaction(f.Manager, victim, UnitFactionType.Player,
            out Entity selected, out float distance);
        Assert.That(selected, Is.EqualTo(f.Player));
        Assert.That(distance, Is.EqualTo(2));
        f.Tick(8);
        UnitPerceptionQueryUtility.TryGetNearestByFaction(f.Manager, victim, UnitFactionType.Player,
            out selected, out _);
        Assert.That(selected, Is.EqualTo(closerPlayer));
    }

    [Test]
    public void DamageMemoryWorksWithZeroNormalSearchRadius()
    {
        using var f = new Fixture();
        Entity victim = f.Monster();
        var perception = f.Memory(victim);
        perception.SearchRadius = 0;
        f.Manager.SetComponentData(victim, perception);
        f.Damage(victim);
        f.Tick(1);
        Assert.That(UnitPerceptionQueryUtility.GetCountByFaction(f.Manager, victim, UnitFactionType.Player), Is.EqualTo(1));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly World World = new("Damage aggro regression");
        public EntityManager Manager => World.EntityManager;
        public readonly Entity Player;
        private readonly SystemHandle querySystem;
        private readonly SystemHandle perceptionSystem;

        public Fixture()
        {
            GameSingletonUtility.Create(Manager, GameWorldRole.Server, GameSceneMode.Dungeon);
            var assembly = typeof(UnitPerceptionComponent).Assembly;
            querySystem = World.GetOrCreateSystem(assembly.GetType("UnitQueryBuildSystem", true));
            perceptionSystem = World.GetOrCreateSystem(assembly.GetType("UnitPerceptionSystem", true));
            Player = PlayerAt(30);
        }

        public Entity PlayerAt(float x)
        {
            Entity player = Actor(UnitFactionType.Player, x);
            return player;
        }

        public Entity Owner()
        {
            Entity owner = Manager.CreateEntity(typeof(UnitVariableComponent));
            Manager.AddBuffer<UnitVariableElement>(owner);
            Manager.AddBuffer<UnitVariableConsumerElement>(owner);
            return owner;
        }

        public Entity Monster(Entity owner = default, bool patrol = false)
        {
            Entity monster = Actor(UnitFactionType.Enemy, 0);
            Manager.AddComponentData(monster, new UnitPerceptionComponent { SearchRadius = 8 });
            Manager.AddBuffer<UnitPerceptionUnitElement>(monster);
            Manager.AddComponent<UnitVariableComponent>(monster);
            Manager.AddBuffer<UnitVariableElement>(monster);
            Manager.AddBuffer<UnitVariableConsumerElement>(monster);
            if (owner != Entity.Null)
            {
                Manager.AddComponentData(monster, new UnitOwnerComponent { Owner = owner });
                UnitVariableSource.SetOther(Manager, monster, owner);
            }
            UnitVariableSource.TrySetValue(Manager, monster, DungeonPatrolRuntimeUtility.PatrolMemberKey, UnitValue.FromBool(patrol));
            return monster;
        }

        private Entity Actor(UnitFactionType faction, float x)
        {
            Entity entity = Manager.CreateEntity(typeof(UnitFactionComponent), typeof(LocalTransform),
                typeof(UnitVitalityComponent), typeof(UnitDeathComponent), typeof(DestroyEntityFlag));
            Manager.SetComponentData(entity, new UnitFactionComponent { Value = faction });
            Manager.SetComponentData(entity, new UnitVitalityComponent { CurrentHealth = 100 });
            Manager.SetComponentEnabled<UnitDeathComponent>(entity, false);
            Manager.SetComponentEnabled<DestroyEntityFlag>(entity, false);
            Position(entity, x);
            return entity;
        }

        public UnitPerceptionComponent Memory(Entity unit) => Manager.GetComponentData<UnitPerceptionComponent>(unit);
        public void Position(Entity entity, float x) => Manager.SetComponentData(entity, LocalTransform.FromPosition(x, 0, 0));
        public void Time(double time) => World.SetTime(new TimeData(time, 1f / 60));
        public void Tick(double time)
        {
            Time(time);
            querySystem.Update(World.Unmanaged);
            perceptionSystem.Update(World.Unmanaged);
            Manager.CompleteAllTrackedJobs();
        }
        public SkillContent Context(Entity victim, Entity attacker) => new()
        {
            EntityManager = Manager, HasOriginEntity = true, OriginEntity = attacker,
            HasTargetEntity = true, TargetEntity = victim,
        };
        public void Damage(Entity victim, float amount = 1) =>
            new DamageEffect(new DamageEffectData { FlatDamageBonus = amount, DamageCoefficient = 0 })
                .Execute(Context(victim, Player));
        public void Dispose() => World.Dispose();
    }
}
