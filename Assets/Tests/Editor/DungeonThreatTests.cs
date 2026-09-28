using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed class DungeonThreatTests
{
    [Test]
    public void AuthoredGraphsCompileAndFixedMapThreatTotals300()
    {
        var rows = LoadRows();
        Assert.That(StateScriptCompiler.TryBuildRegistry(rows, out var registry, out string error), Is.True, error);
        registry.Dispose();
        var trees = JsonConvert.DeserializeObject<BehaviorTable>(File.ReadAllText(
            Path.Combine(Application.dataPath, "Res/Data/BehaviorTreeDataTable.json"))).Rows;
        Assert.That(BehaviorTreeCompiler.TryBuildRegistry(trees, out var behaviorRegistry, out error), Is.True, error);
        behaviorRegistry.Dispose();
        var theme = JsonConvert.DeserializeObject<ThemeTable>(File.ReadAllText(
            Path.Combine(Application.dataPath, "Res/Data/DungeonThemeDataTable.json"))).Rows[0];
        var content = theme.OpenField.Content;
        int area = theme.OpenField.Terrain.Width * theme.OpenField.Terrain.Height;
        int small = Mathf.Clamp(area / 1600, 4, 12);
        int medium = Mathf.Clamp(area / 3500, 2, 6);
        int large = Mathf.Clamp(area / 7000, 1, 3);
        Assert.That(content.WildSquadCount * content.WildSquadClearThreat +
                    small * content.InterestClearThreat.x + medium * content.InterestClearThreat.y +
                    large * content.InterestClearThreat.z, Is.EqualTo(300f));
        Assert.That(content.PatrolReturnThreat, Is.EqualTo(40f));
    }

    [Test]
    public void ClearsAreImmediateOnceOnlyAndIgnoreSurvivingPatrols()
    {
        using var f = new ThreatWorld();
        Entity point = f.Parent(30, 8), wild = f.Parent(36, 3);
        Entity guard = f.Member(point, true), wildMember = f.Member(wild, true);
        f.Member(point, false);
        f.Tick(3);
        Assert.That(f.Threat, Is.Zero, "Disabled death components on living units must not count as dead.");
        f.Kill(guard);
        f.Kill(wildMember);
        f.Tick(3);
        Assert.That(f.Threat, Is.EqualTo(11));
        Assert.That(DungeonPatrolRuntimeUtility.IsEncounterDead(f.Manager, point), Is.True);
        Assert.That(DungeonPatrolRuntimeUtility.IsEncounterDead(f.Manager, wild), Is.True);
        f.Tick(10);
        Assert.That(f.Threat, Is.EqualTo(11), "Terminal state must not add threat again.");
    }

    [Test]
    public void EmptyOrUnreadyParentsDoNotAwardClearThreat()
    {
        using var f = new ThreatWorld();
        f.Parent(36, 3);
        Entity point = f.Parent(30, 8);
        Entity member = f.Member(point, true);
        f.Bool(point, "dungeon.encounter.ready", false);
        f.Kill(member);
        f.Tick(5);
        Assert.That(f.Threat, Is.Zero);
        f.Bool(point, "dungeon.encounter.ready", true);
        f.Tick(3);
        Assert.That(f.Threat, Is.EqualTo(8));
    }

    [Test]
    public void CombatReturnAwards40OnlyWhenAllSurvivorsArrive()
    {
        using var f = new ThreatWorld();
        Entity point = f.Parent(30, 8);
        f.Member(point, true);
        Entity first = f.Member(point, false), second = f.Member(point, false);
        f.Tick(3);
        f.Bool(first, "dungeon.patrol.engaged", true);
        f.Tick(2);
        Assert.That(f.Threat, Is.Zero);
        f.Bool(first, "dungeon.patrol.engaged", false);
        f.Tick(2);
        Assert.That(f.Manager.GetComponentData<DungeonInterestPointComponent>(point).PatrolTarget, Is.EqualTo(point));
        f.Number(point, "dungeon.patrol.reachedCount", 1);
        f.Tick(10);
        Assert.That(f.Threat, Is.Zero, "One missing survivor blocks return settlement.");
        f.Number(point, "dungeon.patrol.reachedCount", 2);
        f.Tick(10);
        Assert.That(f.Threat, Is.EqualTo(40));
        f.Tick(10);
        Assert.That(f.Threat, Is.EqualTo(40));
        f.Bool(second, "dungeon.patrol.engaged", true);
        f.Tick(2);
        f.Bool(second, "dungeon.patrol.engaged", false);
        f.Tick(2);
        f.Number(point, "dungeon.patrol.reachedCount", 2);
        f.Tick(10);
        Assert.That(f.Threat, Is.EqualTo(80), "A later combat trip can settle again.");
    }

    [Test]
    public void WipedPatrolOrDeadHomeCannotSettleCombatReturn()
    {
        using var f = new ThreatWorld();
        Entity point = f.Parent(30, 8);
        Entity guard = f.Member(point, true), patrol = f.Member(point, false);
        f.Tick(3);
        f.Bool(patrol, "dungeon.patrol.engaged", true);
        f.Tick(2);
        f.Kill(patrol);
        f.Tick(3);
        f.Member(point, false);
        f.Tick(3);
        f.Bool(point, "dungeon.patrol.returningHome", true);
        f.Number(point, "dungeon.patrol.reachedCount", 1);
        f.Tick(10);
        Assert.That(f.Threat, Is.Zero, "New patrol cannot inherit a wiped patrol's pending report.");
        f.Bool(point, "dungeon.patrol.hadCombat", true);
        f.Bool(point, "dungeon.patrol.returningHome", true);
        f.Number(point, "dungeon.patrol.reachedCount", 1);
        f.Kill(guard);
        f.Tick(10);
        Assert.That(f.Threat, Is.EqualTo(8), "A dead home awards only its clear threat.");
    }

    [Test]
    public void RosterMinimumsConsumeBudgetAndFinalDrawMayOvershoot()
    {
        using var choices = new NativeArray<UnitRosterUtility.Choice>(new[]
        {
            new UnitRosterUtility.Choice { Unit = "Bat", MinCount = 1, Cost = 1, Weight = 1 },
            new UnitRosterUtility.Choice { Unit = "Werewolf", MinCount = 1, Cost = 3, Weight = 1 },
        }, Allocator.Temp);
        NativeList<int> result = new(Allocator.Temp);
        try
        {
            foreach (float budget in new[] { 1f, 4f, 6f, 8f, 10f, 12.5f })
            {
                var random = Unity.Mathematics.Random.CreateFromIndex(42);
                Assert.That(UnitRosterUtility.Build(choices, budget, 1f, ref random, ref result), Is.True);
                Assert.That(result[0], Is.EqualTo(0));
                Assert.That(result[1], Is.EqualTo(1));
                int cost = 0;
                foreach (int index in result) cost += choices[index].Cost;
                Assert.That(cost, Is.GreaterThanOrEqualTo(math.max(4, budget)));
                Assert.That(cost, Is.LessThan(math.max(4, budget) + 3));
                if (budget <= 4) Assert.That(result.Length, Is.EqualTo(2));
            }
            var invalidRandom = Unity.Mathematics.Random.CreateFromIndex(1);
            Assert.That(UnitRosterUtility.Build(choices, float.NaN, 1f, ref invalidRandom, ref result), Is.False);
            Assert.That(result.Length, Is.Zero);
        }
        finally { result.Dispose(); }
    }

    [Test]
    public void RevengeGraphUsesLinearBudgetAndRebuildsRosterEachWave()
    {
        using var f = new ThreatWorld(true);
        f.Player();
        Entity point = f.Parent(30, 8);
        f.Member(point, true);
        f.RevengeTemplate(point, 2, 20);
        f.Tick(3);
        for (int wave = 0; wave < 4; wave++)
        {
            f.Number(f.Floor, "dungeon.floor.threat", 100);
            f.Tick(1);
            float expected = 20 * (1 + wave * 0.5f);
            Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.budget"), Is.EqualTo(expected));
            Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.spawn.count"), Is.EqualTo(expected));
            Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.wave"), Is.EqualTo(wave + 1));
            Assert.That(f.Threat, Is.Zero);
            Assert.That(f.QueuedSpawns, Is.EqualTo(1));
        }
        // A smaller replacement template must not reuse stale entries or reset the multiplier.
        f.RevengeTemplate(point, 0, 2);
        f.Number(f.Floor, "dungeon.floor.threat", 100);
        f.Tick(1);
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.spawn.count"), Is.EqualTo(6));
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Floor, "dungeon.revenge.spawn.6.unit", out _), Is.False);
    }

    [Test]
    public void RevengeSourceIsLargestLivingPointAndFallsBackWithoutResettingWave()
    {
        using var f = new ThreatWorld(true);
        f.Player();
        Entity large = f.Parent(30, 0), small = f.Parent(30, 0);
        Entity largeGuard = f.Member(large, true);
        f.Member(small, true);
        f.RevengeTemplate(large, 2, 24);
        f.RevengeTemplate(small, 0, 10);
        f.Tick(3);
        f.Number(f.Floor, "dungeon.floor.threat", 100);
        f.Tick(1);
        Assert.That(f.ReadEntity(f.Floor, "dungeon.revenge.origin.0"), Is.EqualTo(large));
        f.Kill(largeGuard);
        // Before the point has written its dead flag, its zero surviving guards already exclude it.
        f.Number(f.Floor, "dungeon.floor.threat", 100);
        f.Tick(1);
        Assert.That(f.ReadEntity(f.Floor, "dungeon.revenge.origin.0"), Is.EqualTo(small));
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.budget"), Is.EqualTo(15));
    }

    [Test]
    public void RevengeGraphDoesNotSpendThreatWithoutPlayerOrLivingSource()
    {
        using var f = new ThreatWorld(true);
        Entity point = f.Parent(30, 0);
        Entity guard = f.Member(point, true);
        f.RevengeTemplate(point, 2, 24);
        f.Number(f.Floor, "dungeon.floor.threat", 100);
        f.Tick(3);
        Assert.That(f.Threat, Is.EqualTo(100));
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.wave"), Is.Zero);
        f.Player();
        f.Kill(guard);
        f.Tick(3);
        Assert.That(f.Threat, Is.EqualTo(100));
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.wave"), Is.Zero);
        Assert.That(f.QueuedSpawns, Is.Zero);
    }

    [Test]
    public void EqualLargestSourcesAreRandomizedAndUnreadyPointsAreExcluded()
    {
        using var f = new ThreatWorld(true);
        f.Player();
        Entity first = f.Parent(30, 0), second = f.Parent(30, 0), unready = f.Parent(30, 0);
        foreach (Entity point in new[] { first, second, unready })
        {
            f.Member(point, true);
            f.RevengeTemplate(point, 2, 2);
        }
        f.Bool(unready, "dungeon.encounter.ready", false);
        f.Tick(3);
        HashSet<Entity> picked = new();
        for (int wave = 0; wave < 20; wave++)
        {
            f.Number(f.Floor, "dungeon.floor.threat", 100);
            f.Tick(1);
            picked.Add(f.ReadEntity(f.Floor, "dungeon.revenge.origin.0"));
        }
        Assert.That(picked, Is.EquivalentTo(new[] { first, second }));
    }

    [Test]
    public void InvalidRosterClearsOldListWithoutQueuingOrSpendingThreat()
    {
        using var f = new ThreatWorld(true);
        f.Player();
        Entity point = f.Parent(30, 0);
        f.Member(point, true);
        f.RevengeTemplate(point, 2, 2);
        f.Tick(3);
        f.Number(f.Floor, "dungeon.floor.threat", 100);
        f.Tick(1);
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.spawn.count"), Is.EqualTo(2));
        f.Number(point, "dungeon.revenge.template.0.cost", 0);
        f.Number(f.Floor, "dungeon.floor.threat", 100);
        f.Tick(1);
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.spawn.count"), Is.Zero);
        Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.wave"), Is.EqualTo(1));
        Assert.That(f.Threat, Is.EqualTo(100));
        Assert.That(f.QueuedSpawns, Is.Zero);
    }

    [TestCase(1, 1f, 1f)]
    [TestCase(5, 1.4f, 1.2f)]
    [TestCase(10, 1.9f, 1.45f)]
    public void FloorDifficultyIsLinearAndLeavesConfigUnchanged(int floorNumber, float health, float budget)
    {
        var config = new DungeonDifficultyData();
        var first = config.Resolve(floorNumber);
        var second = config.Resolve(floorNumber);
        Assert.That(first.HealthMultiplier, Is.EqualTo(health).Within(0.0001f));
        Assert.That(first.BudgetMultiplier, Is.EqualTo(budget).Within(0.0001f));
        Assert.That(second.HealthMultiplier, Is.EqualTo(first.HealthMultiplier));
        Assert.That(config.HealthGrowthPerFloor, Is.EqualTo(0.10f));
        Assert.That(config.BudgetGrowthPerFloor, Is.EqualTo(0.05f));
        Assert.That(config.Resolve(0).HealthMultiplier, Is.EqualTo(1));
    }

    [Test]
    public void RosterAppliesFloorBudgetOnceWithoutScalingMinimumsOrCosts()
    {
        using var choices = new NativeArray<UnitRosterUtility.Choice>(new[]
        {
            new UnitRosterUtility.Choice { Unit = "Bat", MinCount = 4, Cost = 1, Weight = 1 },
        }, Allocator.Temp);
        NativeList<int> result = new(Allocator.Temp);
        try
        {
            var random = Unity.Mathematics.Random.CreateFromIndex(42);
            Assert.That(UnitRosterUtility.Build(choices, 20, 1.2f, ref random, ref result), Is.True);
            Assert.That(result.Length, Is.EqualTo(24));
            Assert.That(UnitRosterUtility.Build(choices, 20, 1.2f, ref random, ref result), Is.True);
            Assert.That(result.Length, Is.EqualTo(24), "Rebuilding must not scale the previous roster.");
            Assert.That(UnitRosterUtility.Build(choices, 1, 1.2f, ref random, ref result), Is.True);
            Assert.That(result.Length, Is.EqualTo(4), "Minimums are fixed, not multiplied.");
            Assert.That(choices[0].Cost, Is.EqualTo(1));
            Assert.That(choices[0].MinCount, Is.EqualTo(4));
            Assert.That(UnitRosterUtility.Build(choices, 20, float.NaN, ref random, ref result), Is.False);
            Assert.That(result.Length, Is.Zero);
        }
        finally { result.Dispose(); }
    }

    [Test]
    public void FloorHealthOnlyAffectsMonstersAndIsAppliedOncePerInstance()
    {
        using var world = new World("Floor health initialization");
        EntityManager manager = world.EntityManager;
        Entity parent = manager.CreateEntity();
        manager.AddComponentData(parent, new DungeonDifficultyData().Resolve(5));
        foreach (UnitFactionType faction in Enum.GetValues(typeof(UnitFactionType)))
        {
            Entity entity = manager.CreateEntity();
            manager.AddComponentData(entity, new UnitFactionComponent { Value = faction });
            manager.AddComponentData(entity, new UnitVitalityComponent
            {
                BaseMaxHealth = 200, CurrentHealth = 200, BaseDefense = 4, BaseHealthRegenPerSecond = 2,
            });
            manager.AddComponentData(entity, new UnitAttackComponent { BaseAttackPower = 10 });
            DungeonDifficultyUtility.Inherit(manager, parent, entity);
            bool isMonster = faction == UnitFactionType.Enemy || faction == UnitFactionType.Boss;
            Assert.That(DungeonDifficultyUtility.ApplyHealth(manager, entity), Is.EqualTo(isMonster));
            var vitality = manager.GetComponentData<UnitVitalityComponent>(entity);
            Assert.That(vitality.BaseMaxHealth, Is.EqualTo(isMonster ? 280 : 200).Within(0.001f));
            Assert.That(vitality.CurrentHealth, Is.EqualTo(vitality.BaseMaxHealth));
            Assert.That(vitality.BaseDefense, Is.EqualTo(4));
            Assert.That(vitality.BaseHealthRegenPerSecond, Is.EqualTo(2));
            Assert.That(manager.GetComponentData<UnitAttackComponent>(entity).BaseAttackPower, Is.EqualTo(10));
            vitality.CurrentHealth = 73;
            manager.SetComponentData(entity, vitality);
            DungeonDifficultyUtility.Inherit(manager, parent, entity);
            Assert.That(DungeonDifficultyUtility.ApplyHealth(manager, entity), Is.False);
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(entity).CurrentHealth, Is.EqualTo(73));
        }
        // A child of an already scaled monster still gets its own one-time initialization.
        var inherited = manager.GetComponentData<DungeonDifficultyComponent>(parent);
        inherited.HealthApplied = 1;
        manager.SetComponentData(parent, inherited);
        Entity child = manager.CreateEntity();
        DungeonDifficultyUtility.Inherit(manager, parent, child);
        Assert.That(manager.GetComponentData<DungeonDifficultyComponent>(child).HealthApplied, Is.Zero);
    }

    [Test]
    public void SpawnInitializationScalesBeforeRestoringSavedHealthAndDoesNotRescale()
    {
        using var world = new World("Floor health restore");
        EntityManager manager = world.EntityManager;
        Entity run = manager.CreateEntity();
        manager.AddComponentObject(run, new DungeonRunComponent
        {
            Units = new List<UnitRuntimeData> { new() { SaveId = 17, Health = 73 } },
        });
        var system = world.GetOrCreateSystemManaged<DungeonSpawnInitializationSystem>();
        for (int reload = 0; reload < 2; reload++)
        {
            Entity monster = manager.CreateEntity();
            manager.AddComponentData(monster, new DungeonDifficultyData().Resolve(5));
            manager.AddComponentData(monster, new UnitFactionComponent { Value = UnitFactionType.Enemy });
            manager.AddComponentData(monster, new UnitVitalityComponent { BaseMaxHealth = 200, CurrentHealth = 200 });
            manager.AddComponentData(monster, new DungeonMonsterSpawnComponent { SaveId = 17 });
            manager.AddComponentData(monster, new UnitSpawnInitializationComponent { RestoreRuntimeState = 1 });
            system.Update();
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(monster).BaseMaxHealth, Is.EqualTo(280).Within(0.001f));
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(monster).CurrentHealth, Is.EqualTo(73));
            manager.AddComponent<UnitSpawnInitializationComponent>(monster);
            system.Update();
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(monster).BaseMaxHealth, Is.EqualTo(280).Within(0.001f));
            Assert.That(manager.GetComponentData<UnitVitalityComponent>(monster).CurrentHealth, Is.EqualTo(73));
            manager.DestroyEntity(monster);
        }
    }

    [Test]
    public void RevengeWavesMultiplyFloorBudgetExactlyOnce()
    {
        using var f = new ThreatWorld(true, 5);
        f.Player();
        Entity point = f.Parent(30, 0);
        f.Member(point, true);
        f.RevengeTemplate(point, 2, 20);
        f.Tick(3);
        for (int wave = 0; wave < 3; wave++)
        {
            f.Number(f.Floor, "dungeon.floor.threat", 100);
            f.Tick(1);
            Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.budget"), Is.EqualTo(20 * (1 + wave * 0.5f)));
            Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.spawn.budget"), Is.EqualTo(24 * (1 + wave * 0.5f)).Within(0.001f));
            Assert.That(f.ReadNumber(f.Floor, "dungeon.revenge.spawn.count"), Is.EqualTo(24 + wave * 12));
            Assert.That(f.ReadNumber(point, "dungeon.revenge.template.costLimit"), Is.EqualTo(20));
        }
    }

    [Test]
    public void PatrolRosterInheritsFloorBudgetWithoutChangingItsTemplate()
    {
        using var f = new ThreatWorld(false, 5);
        Entity point = f.Parent(30, 0);
        f.Member(point, true);
        f.Number(point, "dungeon.patrol.template.costLimit", 4);
        f.Number(point, "dungeon.patrol.template.count", 1);
        f.Number(point, "dungeon.patrol.template.0.minCount", 1);
        f.Number(point, "dungeon.patrol.template.0.cost", 1);
        f.Number(point, "dungeon.patrol.template.0.weight", 1);
        UnitVariableSource.TrySetValue(f.Manager, point, "dungeon.patrol.template.0.unit", UnitValue.FromString("Bat"));
        f.Tick(185);
        Assert.That(f.ReadNumber(point, "dungeon.patrol.spawn.budget"), Is.EqualTo(4.8f).Within(0.001f));
        Assert.That(f.ReadNumber(point, "dungeon.patrol.spawn.count"), Is.EqualTo(5));
        Assert.That(f.ReadNumber(point, "dungeon.patrol.template.costLimit"), Is.EqualTo(4));
    }

    [Test]
    public void HealthInitializationDoesNotReclassifyGarrisonAsPatrol()
    {
        using var world = new World("Garrison difficulty initialization");
        EntityManager manager = world.EntityManager;
        Entity point = manager.CreateEntity();
        manager.AddComponentData(point, new DungeonDifficultyData().Resolve(5));
        manager.AddComponentData(point, new DungeonInterestPointComponent { AliveGuardCount = 1 });
        Entity guard = manager.CreateEntity();
        manager.AddComponentData(guard, new UnitOwnerComponent { Owner = point });
        manager.AddComponentData(guard, new UnitFactionComponent { Value = UnitFactionType.Enemy });
        manager.AddComponentData(guard, new UnitVitalityComponent { BaseMaxHealth = 200, CurrentHealth = 200 });
        manager.AddComponentData(guard, new DungeonMonsterSpawnComponent { CountsAsGuard = 1 });
        manager.AddComponent<UnitSpawnInitializationComponent>(guard);
        world.GetOrCreateSystemManaged<DungeonSpawnInitializationSystem>().Update();
        Assert.That(manager.GetComponentData<UnitVitalityComponent>(guard).BaseMaxHealth, Is.EqualTo(280).Within(0.001f));
        Assert.That(manager.GetComponentData<DungeonMonsterSpawnComponent>(guard).CountsAsPatrol, Is.Zero);
        Assert.That(manager.GetComponentData<DungeonInterestPointComponent>(point).PatrolUnitCount, Is.Zero);
        Assert.That(manager.GetComponentData<DungeonInterestPointComponent>(point).AliveGuardCount, Is.EqualTo(1));
    }

    private sealed class BehaviorTable { public List<BehaviorTreeData> Rows = new(); }
    private sealed class ScriptTable { public List<StateScriptData> Rows = new(); }
    private sealed class ThemeTable { public List<DungeonThemeData> Rows = new(); }
    private static List<StateScriptData> LoadRows() => JsonConvert.DeserializeObject<ScriptTable>(File.ReadAllText(
        Path.Combine(Application.dataPath, "Res/Data/StateScriptDataTable.json"))).Rows;

    [TestCase(30, 8)]
    [TestCase(36, 3)]
    public void ThreatNotificationCapturesBeforeAfterAndCycleBeforeAnyReset(int unitId, float clearThreat)
    {
        using var f = new ThreatWorld();
        Entity point = f.Parent(unitId, clearThreat);
        Entity guard = f.Member(point, true);
        f.Tick(2);
        f.Number(f.Floor, "dungeon.floor.threat", 95);
        f.Number(f.Floor, "dungeon.revenge.wave", 2);
        f.Kill(guard);
        f.Tick(1);
        Entity queue = StateScriptManagedCommandQueueUtility.GetOrCreateEntity(f.Manager);
        var signals = new List<StateScriptManagedCommandElement>();
        foreach (var command in f.Manager.GetBuffer<StateScriptManagedCommandElement>(queue))
            if (command.Type == StateScriptManagedCommandType.NotifyUI) signals.Add(command);
        Assert.That(signals.Count, Is.EqualTo(1));
        Assert.That(signals[0].Position, Is.EqualTo(new float3(95, 95 + clearThreat, 2)));
        f.Number(f.Floor, "dungeon.floor.threat", 0);
        f.Number(f.Floor, "dungeon.revenge.wave", 3);
        foreach (var command in f.Manager.GetBuffer<StateScriptManagedCommandElement>(queue))
            if (command.Type == StateScriptManagedCommandType.NotifyUI)
                Assert.That(command.Position, Is.EqualTo(signals[0].Position), "Presentation must not re-read the reset blackboard.");
    }

    private sealed class ThreatWorld : IDisposable
    {
        private readonly World world = new("Dungeon threat regression");
        private readonly BlobAssetReference<StateScriptRuntimeRegistryBlob> registry;
        private readonly StateScriptSystem system;
        private readonly Entity floor;
        private readonly Entity queryTree;
        private double elapsed;
        public EntityManager Manager => world.EntityManager;
        public float Threat => ReadNumber(floor, "dungeon.floor.threat");
        public Entity Floor => floor;
        public int QueuedSpawns
        {
            get
            {
                Entity queue = StateScriptManagedCommandQueueUtility.GetOrCreateEntity(Manager);
                int count = 0;
                foreach (var command in Manager.GetBuffer<StateScriptManagedCommandElement>(queue))
                    if (command.Type == StateScriptManagedCommandType.SpawnUnit) count++;
                return count;
            }
        }

        public ThreatWorld(bool withFloorScript = false, int floorNumber = 1)
        {
            GameSingletonUtility.Create(Manager, GameWorldRole.Standalone, GameSceneMode.Dungeon);
            Assert.That(StateScriptCompiler.TryBuildRegistry(LoadRows().Where(r => r.Id == 30 || r.Id == 35 || r.Id == 36).ToArray(),
                out registry, out string error), Is.True, error);
            Entity entity = Manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent));
            Manager.SetComponentData(entity, new StateScriptRuntimeRegistryComponent { Value = registry });
            // The state-script job requires a valid query snapshot even when no
            // spatial units are needed by these threat-settlement tests.
            queryTree = Manager.CreateEntity();
            Manager.AddBuffer<UnitQueryNode>(queryTree).EnsureCapacity(1);
            Manager.AddBuffer<UnitQueryEntry>(queryTree).EnsureCapacity(1);
            Entity querySingleton = Manager.CreateEntity(typeof(UnitQuerySingleton));
            Manager.SetComponentData(querySingleton, new UnitQuerySingleton { TreeEntity = queryTree });
            floor = Blackboard();
            Manager.AddComponentData(floor, new DungeonDifficultyData().Resolve(floorNumber));
            Number(floor, "dungeon.floor.threat", 0);
            Number(floor, "dungeon.revenge.wave", 0);
            if (withFloorScript) AddScript(floor, 35);
            system = world.GetOrCreateSystemManaged<StateScriptSystem>();
        }

        private Entity Blackboard()
        {
            Entity entity = Manager.CreateEntity(typeof(UnitVariableComponent), typeof(LocalTransform));
            Manager.SetComponentData(entity, LocalTransform.Identity);
            Manager.AddBuffer<UnitVariableElement>(entity);
            Manager.AddBuffer<UnitVariableConsumerElement>(entity);
            return entity;
        }

        public Entity Parent(int id, float threat)
        {
            Entity entity = Blackboard();
            DungeonDifficultyUtility.Inherit(Manager, floor, entity);
            UnitVariableSource.SetOther(Manager, entity, floor);
            DungeonPatrolRuntimeUtility.InitializeEncounterVariables(Manager, entity, threat);
            Bool(entity, "dungeon.encounter.ready", true);
            Bool(entity, "dungeon.patrol.active", true);
            Bool(entity, "dungeon.patrol.hadCombat", false);
            Bool(entity, "dungeon.patrol.returningHome", false);
            Number(entity, "dungeon.threat.onPatrolReturn", 40);
            if (id == 30) Manager.AddComponentData(entity, new DungeonInterestPointComponent { EncounterReady = 1, PatrolEnabled = 1 });
            Manager.AddComponentData(entity, new UnitFactionComponent { Value = UnitFactionType.Interactable });
            AddScript(entity, id);
            return entity;
        }

        private void AddScript(Entity entity, int id)
        {
            int index = StateScriptCompiler.FindUnitIndex(in registry, id);
            ref var unit = ref registry.Value.Units[index];
            Manager.AddComponentData(entity, new UnitStateScriptComponent { UnitDataId = id, DefinitionIndex = index });
            var graphs = Manager.AddBuffer<StateScriptGraphStateElement>(entity);
            int start = 0;
            for (int i = 0; i < unit.Graphs.Length; i++)
            {
                graphs.Add(new StateScriptGraphStateElement { NodeStateStart = start });
                start += unit.Graphs[i].Nodes.Length;
            }
            var nodes = Manager.AddBuffer<StateScriptNodeStateElement>(entity);
            for (int i = 0; i < start; i++) nodes.Add(default);
            Manager.AddBuffer<StateScriptSourceCommandElement>(entity);
            Manager.AddBuffer<StateScriptSourceCommandArgumentElement>(entity);
            Manager.AddBuffer<StateScriptExternalResultElement>(entity);
        }

        public void Player()
        {
            Entity entity = Blackboard();
            Manager.AddComponentData(entity, new UnitFactionComponent { Value = UnitFactionType.Player });
        }

        public void RevengeTemplate(Entity point, int size, int budget)
        {
            Number(point, "dungeon.encounter.size", size);
            Number(point, "dungeon.revenge.template.costLimit", budget);
            Number(point, "dungeon.revenge.template.count", 1);
            UnitVariableSource.TrySetValue(Manager, point, "dungeon.revenge.template.0.unit", UnitValue.FromString("Bat"));
            Number(point, "dungeon.revenge.template.0.minCount", 1);
            Number(point, "dungeon.revenge.template.0.cost", 1);
            Number(point, "dungeon.revenge.template.0.weight", 1);
        }

        public Entity Member(Entity parent, bool guard)
        {
            Entity entity = Blackboard();
            UnitVariableSource.SetOther(Manager, entity, parent);
            Manager.AddComponent<UnitDeathComponent>(entity);
            Manager.SetComponentEnabled<UnitDeathComponent>(entity, false);
            Bool(entity, DungeonPatrolRuntimeUtility.GuardMemberKey, guard);
            Bool(entity, DungeonPatrolRuntimeUtility.PatrolMemberKey, !guard);
            Bool(entity, "dungeon.patrol.engaged", false);
            if (guard) Bool(parent, "dungeon.encounter.populated", true);
            return entity;
        }

        public void Kill(Entity entity) => Manager.SetComponentEnabled<UnitDeathComponent>(entity, true);
        public void Bool(Entity entity, string key, bool value) => UnitVariableSource.TrySetValue(Manager, entity, key, UnitValue.FromBool(value));
        public void Number(Entity entity, string key, float value) => UnitVariableSource.TrySetValue(Manager, entity, key, UnitValue.FromFloat(value));
        public Entity ReadEntity(Entity entity, string key)
        {
            Assert.That(UnitVariableSource.TryGetValue(Manager, entity, key, out var value), Is.True);
            Assert.That(value.TryGetEntity(out Entity result), Is.True);
            return result;
        }
        public float ReadNumber(Entity entity, string key)
        {
            Assert.That(UnitVariableSource.TryGetValue(Manager, entity, key, out var value), Is.True);
            Assert.That(value.TryGetNumber(out float result), Is.True);
            return result;
        }
        public void Tick(int count)
        {
            for (int i = 0; i < count; i++)
            {
                world.SetTime(new TimeData(++elapsed, 1f));
                RebuildQuerySnapshot();
                system.Update();
                Manager.CompleteAllTrackedJobs();
            }
        }
        private void RebuildQuerySnapshot()
        {
            using EntityQuery query = Manager.CreateEntityQuery(typeof(LocalTransform), typeof(UnitFactionComponent));
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            var entries = Manager.GetBuffer<UnitQueryEntry>(queryTree);
            entries.Clear();
            foreach (Entity entity in entities)
            {
                entries.Add(new UnitQueryEntry
                {
                    Entity = entity, Position = Manager.GetComponentData<LocalTransform>(entity).Position,
                    Faction = Manager.GetComponentData<UnitFactionComponent>(entity).Value,
                    UnitDataId = Manager.HasComponent<UnitStateScriptComponent>(entity)
                        ? Manager.GetComponentData<UnitStateScriptComponent>(entity).UnitDataId : -1,
                });
            }
            var nodes = Manager.GetBuffer<UnitQueryNode>(queryTree);
            nodes.Clear();
            nodes.Add(new UnitQueryNode { Min = new float2(-1000), Max = new float2(1000), Count = entries.Length, FirstChildIndex = -1 });
        }
        public void Dispose() { world.Dispose(); registry.Dispose(); }
    }
}
