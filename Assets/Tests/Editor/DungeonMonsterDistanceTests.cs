using System;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public sealed class DungeonMonsterDistanceTests
{
    [Test]
    public void ActiveCountsExplainDistanceBandsAndFarProtection()
    {
        using var f = new Fixture();
        f.Monster(10);
        f.Monster(40);
        f.Monster(100);
        Entity buffed = f.Monster(100);
        f.Manager.AddBuffer<UnitBuffElement>(buffed).Add(new UnitBuffElement { RemainingTime = 100 });
        f.Tick(0);
        Assert.That(f.System.ObserverCount, Is.EqualTo(1));
        Assert.That(f.System.ActiveCount, Is.EqualTo(4));
        Assert.That(f.System.ActiveWithinWakeCount, Is.EqualTo(1));
        Assert.That(f.System.ActiveInBufferCount, Is.EqualTo(1));
        Assert.That(f.System.ActiveFarCount, Is.EqualTo(2));
        Assert.That(f.System.WaitingToSleepCount, Is.EqualTo(1));
        Assert.That(f.System.GetBlockedCount(DungeonMonsterSleepBlockReason.Buff), Is.EqualTo(1));
        f.Tick(1.1);
        Assert.That(f.System.SleepingCount, Is.EqualTo(1));
        Assert.That(f.System.ActiveCount, Is.EqualTo(3));
        Assert.That(f.System.ActiveFarCount, Is.EqualTo(1));
        Assert.That(f.System.WaitingToSleepCount, Is.Zero);
        Assert.That(f.System.GetBlockedCount(DungeonMonsterSleepBlockReason.Buff), Is.EqualTo(1));
    }

    [Test]
    public void SleepingUnitsLeaveTheCombatSpatialTreeAndStopHealthRecovery()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(100);
        f.Manager.SetComponentData(monster, new UnitVitalityComponent
            { CurrentHealth = 37, BaseMaxHealth = 100, BaseHealthRegenPerSecond = 10 });
        f.Tick(0); f.Tick(1.1);
        SystemHandle tree = f.World.GetOrCreateSystem<UnitQueryBuildSystem>();
        SystemHandle recovery = f.World.GetOrCreateSystem<UnitRecoverySystem>();
        tree.Update(f.World.Unmanaged);
        recovery.Update(f.World.Unmanaged);
        f.Manager.CompleteAllTrackedJobs();
        using EntityQuery query = f.Manager.CreateEntityQuery(typeof(UnitQuerySingleton));
        Entity treeEntity = query.GetSingleton<UnitQuerySingleton>().TreeEntity;
        using NativeArray<UnitQueryEntry> sleepingEntries = f.Manager.GetBuffer<UnitQueryEntry>(treeEntity).ToNativeArray(Allocator.Temp);
        Assert.That(sleepingEntries.Any(entry => entry.Entity == monster), Is.False);
        Assert.That(f.Manager.GetComponentData<UnitVitalityComponent>(monster).CurrentHealth, Is.EqualTo(37));
        f.Position(f.Player, 100); f.Tick(1.2);
        tree.Update(f.World.Unmanaged);
        recovery.Update(f.World.Unmanaged);
        f.Manager.CompleteAllTrackedJobs();
        using NativeArray<UnitQueryEntry> awakeEntries = f.Manager.GetBuffer<UnitQueryEntry>(treeEntity).ToNativeArray(Allocator.Temp);
        Assert.That(awakeEntries.Any(entry => entry.Entity == monster), Is.True);
        Assert.That(f.Manager.GetComponentData<UnitVitalityComponent>(monster).CurrentHealth, Is.GreaterThan(37));
    }

    [Test]
    public void SleepingRetainsEntityHealthAndOwnershipWithoutDeathOrDrops()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(80);
        Entity owner = f.Manager.CreateEntity(typeof(DungeonInterestPointComponent));
        f.Manager.SetComponentData(owner, new DungeonInterestPointComponent { AliveGuardCount = 1, PatrolUnitCount = 2 });
        f.Manager.AddComponentData(monster, new UnitOwnerComponent { Owner = owner });
        f.Tick(0); f.Tick(1.1);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.True);
        Assert.That(f.Manager.Exists(monster), Is.True);
        Assert.That(f.Manager.GetComponentData<UnitVitalityComponent>(monster).CurrentHealth, Is.EqualTo(37));
        Assert.That(f.Manager.IsComponentEnabled<UnitDeathComponent>(monster), Is.False);
        Assert.That(f.Manager.IsComponentEnabled<DestroyEntityFlag>(monster), Is.False);
        Assert.That(f.Manager.HasComponent<UnitOwnerMemberDeathEvent>(monster), Is.False);
        Assert.That(f.Manager.GetComponentData<DungeonInterestPointComponent>(owner).AliveGuardCount, Is.EqualTo(1));
        f.Position(f.Player, 80); f.Tick(1.2);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.False);
        Assert.That(f.Manager.GetComponentData<UnitOwnerComponent>(monster).Owner, Is.EqualTo(owner));
        Assert.That(f.Manager.GetComponentData<UnitVitalityComponent>(monster).CurrentHealth, Is.EqualTo(37));
        Assert.That(f.Manager.GetComponentData<DungeonInterestPointComponent>(owner).PatrolUnitCount, Is.EqualTo(2));
    }

    [Test]
    public void EveryPlayerProtectsNearbyMonstersAndThereIsNoPopulationLimit()
    {
        using var f = new Fixture();
        Entity secondPlayer = f.Observer(100);
        for (int i = 0; i < 150; i++) f.Monster(100);
        f.Tick(0); f.Tick(2);
        Assert.That(f.System.ActiveCount, Is.EqualTo(150));
        Assert.That(f.System.SleepingCount, Is.Zero);
        f.Position(secondPlayer, 0); f.Tick(3); f.Tick(4.1);
        Assert.That(f.System.SleepingCount, Is.EqualTo(150));
    }

    [Test]
    public void HysteresisAndDelayPreventBoundaryThrashing()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(46);
        f.Tick(0); f.Tick(0.5);
        Assert.That(f.System.SleepingCount, Is.Zero);
        f.Position(monster, 44); f.Tick(0.6);
        f.Position(monster, 46); f.Tick(1); f.Tick(1.5);
        Assert.That(f.System.SleepingCount, Is.Zero);
        f.Tick(2.1);
        Assert.That(f.System.SleepingCount, Is.EqualTo(1));
        f.Position(f.Player, 10); f.Tick(2.2); // 36: between the two radii.
        Assert.That(f.System.SleepingCount, Is.EqualTo(1));
        f.Position(f.Player, 20); f.Tick(2.3);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.False);
    }

    [Test]
    public void EffectsCombatAndRecentDamagePreventSleeping()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(80);
        f.Manager.AddBuffer<UnitBuffElement>(monster).Add(new UnitBuffElement { RemainingTime = 4 });
        f.Tick(0); f.Tick(2);
        Assert.That(f.System.SleepingCount, Is.Zero);
        f.Manager.GetBuffer<UnitBuffElement>(monster).Clear();
        f.Tick(3);
        f.Manager.SetComponentData(monster, new UnitVitalityComponent { CurrentHealth = 20 });
        f.Tick(4.1);
        Assert.That(f.System.SleepingCount, Is.Zero);
        f.Tick(4.2); f.Tick(5.3);
        Assert.That(f.System.SleepingCount, Is.EqualTo(1));
        f.Manager.GetBuffer<UnitBuffElement>(monster).Add(new UnitBuffElement { RemainingTime = 2 });
        f.Tick(5.4);
        Assert.That(f.System.SleepingCount, Is.Zero);
    }

    [Test]
    public void DamageMemoryWakesSleepingSquadMemberAndProtectsUntilExpired()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(80);
        f.Manager.AddComponentData(monster, new UnitPerceptionComponent { SearchRadius = 8 });
        f.Tick(0); f.Tick(2);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.True);
        UnitDamageAggroUtility.NotifyDamage(f.Manager, monster, f.Player);
        f.Tick(2.1);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.False);
        f.Tick(9.9);
        Assert.That(f.System.GetBlockedCount(DungeonMonsterSleepBlockReason.Combat), Is.EqualTo(1));
        f.Tick(10.1); f.Tick(11.2);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.True);
    }

    [Test]
    public void TerminalMonstersAreNeverRecreatedAndExternalDisabledEntitiesStayDisabled()
    {
        using var f = new Fixture();
        Entity dead = f.Monster(80), external = f.Monster(1);
        f.Manager.SetComponentEnabled<UnitDeathComponent>(dead, true);
        f.Manager.SetEnabled(external, false);
        f.Tick(0); f.Tick(2);
        Assert.That(f.Manager.HasComponent<Disabled>(dead), Is.False);
        Assert.That(f.Manager.HasComponent<Disabled>(external), Is.True);
        f.Manager.DestroyEntity(dead); f.Position(f.Player, 80); f.Tick(3);
        Assert.That(f.Manager.Exists(dead), Is.False);
    }

    [Test]
    public void SleepDespawnAndResumeUseDifferentNetworkLifetimesAndPreserveHealth()
    {
        using var f = new Fixture();
        NetworkEntitySpawnUtility.EnsureSpawnQueue(f.Manager);
        Entity monster = f.Monster(80);
        Guid original = Guid.NewGuid();
        f.Manager.AddComponentData(monster, new NetworkIdentityComponent { id = original });
        f.Manager.AddComponentObject(monster, new NetworkEntitySpawnInfoComponent
        {
            entityInfo = new NetworkEntitySpawnInfo { unitId = original, prefabName = "TestMonster" },
        });
        f.Tick(0); f.Tick(1.1);
        using EntityQuery query = f.Manager.CreateEntityQuery(typeof(NetworkEntitySpawnQueueComponent));
        var queue = f.Manager.GetComponentObject<NetworkEntitySpawnQueueComponent>(query.GetSingletonEntity());
        Assert.That(queue.sleepingEntityIds, Is.EqualTo(new[] { original }));
        Assert.That(NetworkEntitySpawnUtility.CreateSnapshotInfos(f.Manager), Is.Empty);
        f.Position(f.Player, 80); f.Tick(1.2);
        Assert.That(queue.entityInfos.Count, Is.EqualTo(1));
        Assert.That(queue.entityInfos[0].unitId, Is.Not.EqualTo(original));
        Assert.That(queue.entityInfos[0].health, Is.EqualTo(37));
        Assert.That(queue.entityInfos[0].x, Is.EqualTo(80));
        f.Tick(1.3);
        Assert.That(queue.entityInfos.Count, Is.EqualTo(1));
        Assert.That(NetworkEntitySpawnUtility.CreateSnapshotInfos(f.Manager).Length, Is.EqualTo(1));
    }

    [Test]
    public void SleepingUnitsAreSavedAndRemovedOnFloorCleanup()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(80);
        f.Manager.AddComponentData(monster, new DungeonMonsterSpawnComponent { SaveId = 42 });
        f.Tick(0); f.Tick(1.1);
        var run = new DungeonRunData();
        typeof(GameRuntimeStateUtility).GetMethod("CaptureDungeonRuntimeState", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { f.Manager, run });
        Assert.That(run.Units.Single(unit => unit.SaveId == 42).Health, Is.EqualTo(37));
        typeof(DungeonSceneRuntimeBuilder).GetMethod("DestroyRuntimeOwnedEntities", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { f.Manager });
        Assert.That(f.Manager.Exists(monster), Is.False);
    }

    [Test]
    public void DisablingFeatureWakesAllSleepingMonsters()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(80);
        f.Tick(0); f.Tick(1.1);
        f.System.Settings.MonsterDistanceSleepEnabled = false;
        f.Tick(1.2);
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.False);
    }

    [Test]
    public void DormantPatrolStillFollowsNavigationAroundWalls()
    {
        using var f = new Fixture();
        Entity monster = f.Monster(4.5f, 2.5f);
        Entity target = f.Manager.CreateEntity(typeof(LocalTransform));
        f.Manager.SetComponentData(target, LocalTransform.FromPosition(16.5f, 2.5f, 0));
        Entity owner = f.Manager.CreateEntity(typeof(DungeonInterestPointComponent), typeof(UnitVariableComponent));
        f.Manager.AddBuffer<UnitVariableElement>(owner);
        f.Manager.SetComponentData(owner, new DungeonInterestPointComponent
            { PatrolTarget = target, PatrolSpeed = 4, ArrivalDistance = 0.75f, PatrolUnitCount = 1 });
        f.Manager.AddComponentData(monster, new UnitOwnerComponent { Owner = owner });
        f.Manager.AddComponent<UnitVariableComponent>(monster);
        f.Manager.AddBuffer<UnitVariableElement>(monster);
        UnitVariableSource.TrySetValue(f.Manager, monster, DungeonPatrolRuntimeUtility.PatrolMemberKey, UnitValue.FromBool(true));
        UnitVariableSource.TrySetValue(f.Manager, owner, DungeonPatrolRuntimeUtility.PatrolActiveKey, UnitValue.FromBool(true));
        f.Manager.AddComponentData(monster, new UnitNavigationComponent { ClearanceRadius = 0, WaypointTolerance = 0.05f });
        f.Manager.AddBuffer<UnitNavigationPathElement>(monster);
        f.Manager.AddComponentData(monster, new UnitMoveComponent { BaseMoveSpeed = 4, CommandMoveSpeed = -1 });
        Entity map = f.Manager.CreateEntity(typeof(DungeonNavigationMapComponent));
        f.Manager.SetComponentData(map, new DungeonNavigationMapComponent { Width = 24, Height = 16, CellSize = 1, Version = 1 });
        var words = f.Manager.AddBuffer<DungeonNavigationCollisionWord>(map);
        words.ResizeUninitialized(6);
        for (int i = 0; i < words.Length; i++) words[i] = default;
        for (int y = 0; y < 10; y++)
        {
            int bit = y * 24 + 10;
            words[bit / 64] = new DungeonNavigationCollisionWord { Value = words[bit / 64].Value | (1UL << (bit % 64)) };
        }
        f.Position(f.Player, -100);
        f.Tick(0); f.Tick(1.1);
        SystemHandle navigation = f.World.GetOrCreateSystem<UnitNavigationSystem>();
        bool routedAboveWall = false;
        for (int i = 0; i < 40; i++)
        {
            f.Tick(1.2 + i * 0.5);
            navigation.Update(f.World.Unmanaged);
            f.Manager.CompleteAllTrackedJobs();
            float3 position = f.Manager.GetComponentData<LocalTransform>(monster).Position;
            Assert.That((int)math.floor(position.x) == 10 && position.y < 10, Is.False, "Must not travel through the wall.");
            routedAboveWall |= position.y >= 10;
        }
        Assert.That(routedAboveWall, Is.True);
        Assert.That(math.distance(f.Manager.GetComponentData<LocalTransform>(monster).Position.xy, new float2(16.5f, 2.5f)), Is.LessThan(1));
        Assert.That(f.Manager.HasComponent<Disabled>(monster), Is.True);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly World World = new("Monster distance tests");
        public EntityManager Manager => World.EntityManager;
        public readonly DungeonMonsterDistanceSystem System;
        public readonly Entity Player;
        public Fixture()
        {
            Manager.CreateEntity(typeof(DungeonFloorControllerComponent));
            System = World.CreateSystemManaged<DungeonMonsterDistanceSystem>();
            System.Settings = new DungeonConfig { MonsterSleepDelay = 1, MonsterDistanceCheckInterval = 0.05f };
            Player = Observer(0);
        }
        public Entity Observer(float x)
        {
            Entity entity = Manager.CreateEntity(typeof(UnitFactionComponent), typeof(LocalTransform));
            Manager.SetComponentData(entity, new UnitFactionComponent { Value = UnitFactionType.Player });
            Position(entity, x);
            return entity;
        }
        public Entity Monster(float x, float y = 0)
        {
            Entity entity = Manager.CreateEntity(typeof(DungeonRuntimeOwnedEntity), typeof(UnitFactionComponent),
                typeof(LocalTransform), typeof(UnitVitalityComponent), typeof(UnitDeathComponent), typeof(DestroyEntityFlag));
            Manager.SetComponentData(entity, new UnitFactionComponent { Value = UnitFactionType.Enemy });
            Manager.SetComponentData(entity, LocalTransform.FromPosition(x, y, 0));
            Manager.SetComponentData(entity, new UnitVitalityComponent { CurrentHealth = 37 });
            Manager.SetComponentEnabled<UnitDeathComponent>(entity, false);
            Manager.SetComponentEnabled<DestroyEntityFlag>(entity, false);
            return entity;
        }
        public void Position(Entity entity, float x) => Manager.SetComponentData(entity, LocalTransform.FromPosition(x, 0, 0));
        public void Tick(double time) { World.SetTime(new TimeData(time, 0.05f)); System.Update(); Manager.CompleteAllTrackedJobs(); }
        public void Dispose() => World.Dispose();
    }
}
