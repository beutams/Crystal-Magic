using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.ThirdParty.RVO2;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed class DungeonPatrolMovementTests
{
    private static JArray Rows(string table) => (JArray)JObject.Parse(File.ReadAllText(
        Path.Combine(Application.dataPath, $"Res/Data/{table}DataTable.json")))["Rows"];

    [Test]
    public void AuthoredGraphsCompileAndAllCombatUnitsCanReturnHome()
    {
        BehaviorTreeData[] trees = Rows("BehaviorTree").Select(row => row.ToObject<BehaviorTreeData>()).ToArray();
        Assert.That(BehaviorTreeCompiler.TryBuildRegistry(trees, out var behaviorRegistry, out string error), Is.True, error);
        behaviorRegistry.Dispose();
        foreach (BehaviorTreeData tree in trees.Where(tree => tree.UnitDataId != 3))
        {
            Assert.That(tree.Nodes.Any(node => node.Guid == "shared_guard_return"), Is.True, tree.Name);
            var home = (MoveToBehaviorNodeData)tree.GetNode("shared_guard_return_action");
            Assert.That(home.Destination.GetterKey, Is.EqualTo("unit.variables.getFloat3"));
            Assert.That(home.Destination.Inputs[0].Literal.String, Is.EqualTo(DungeonPatrolRuntimeUtility.GuardHomePositionKey));
            Assert.That(home.StopDistance.Literal.Float, Is.EqualTo(0.15f));
            Assert.That(home.Conditions, Is.Not.Empty, "A running sequence resumes at MoveTo; its conditions must remain active.");
            Assert.That(tree.Nodes.Any(node => node.Guid.StartsWith("shared_patrol_report_")), Is.False, tree.Name);
        }
        StateScriptData[] scripts = Rows("StateScript").Select(row => row.ToObject<StateScriptData>()).ToArray();
        Assert.That(StateScriptCompiler.TryBuildRegistry(scripts, out var scriptRegistry, out error), Is.True, error);
        scriptRegistry.Dispose();
        StateScriptInstanceData patrol = scripts.Single(row => row.Id == 30).Graphs.Single(graph => graph.Name == "Patrol");
        var spawn = (SpawnUnitActionNodeData)patrol.Nodes.Single(node => node.Guid == "interest_point_spawn_patrol");
        Assert.That(spawn.SpawnIntervalSeconds, Is.EqualTo(1f));
        Assert.That(patrol.Nodes.Any(node => node.Guid == "interest_point_member_count_monitor"), Is.False);
        Assert.That(patrol.Edges.Any(edge => edge.OutputNodeGuid == "interest_point_member_count_monitor"), Is.False);
        var patrolJson = Rows("StateScript").Single(row => (int)row["Id"] == 30);
        foreach (string guid in new[] { "interest_point_patrol_missing_monitor", "interest_point_target_reached_monitor" })
            Assert.That(patrolJson["Graphs"].SelectMany(graph => graph["Nodes"]).Single(node => (string)node["Guid"] == guid).ToString(),
                Does.Contain("dungeon.patrol.spawn.pendingCount"));
    }

    [Test]
    public void GuardHomeIsCapturedOnceAndPatrolUnitsDoNotCaptureIt()
    {
        using var f = new PatrolWorld();
        Entity guard = f.Member(f.Point, 3f, false);
        DungeonPatrolRuntimeUtility.CaptureGuardHome(f.Manager, guard);
        f.Position(guard, 5f);
        DungeonPatrolRuntimeUtility.CaptureGuardHome(f.Manager, guard);
        Assert.That(f.Vector(guard, DungeonPatrolRuntimeUtility.GuardHomePositionKey).x, Is.EqualTo(3f));
        Entity patrol = f.Member(f.Point, 3f);
        DungeonPatrolRuntimeUtility.CaptureGuardHome(f.Manager, patrol);
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, patrol, DungeonPatrolRuntimeUtility.GuardHomePositionKey, out _), Is.False);
    }

    [Test]
    public void SpawnBatchEmitsImmediatelyThenOncePerSecondAndNeverCatchesUpInABurst()
    {
        var entries = Enumerable.Range(0, 5).Select(index => new StateScriptSpawnBatch.Entry { UnitName = "Unit" + index }).ToList();
        var node = new StateScriptNodeDefinition { SpawnIntervalSeconds = 1f };
        var batch = new StateScriptSpawnBatch(entries, in node);
        entries.Clear();
        Assert.That(batch.TryTakeNext(0, out var first), Is.True);
        Assert.That(first.UnitName, Is.EqualTo("Unit0"));
        Assert.That(batch.RemainingCount, Is.EqualTo(4));
        Assert.That(batch.TryTakeNext(0.5f, out _), Is.False);
        Assert.That(batch.TryTakeNext(0.5f, out var second), Is.True);
        Assert.That(second.UnitName, Is.EqualTo("Unit1"));
        Assert.That(batch.TryTakeNext(20f, out _), Is.True);
        Assert.That(batch.RemainingCount, Is.EqualTo(2));
        Assert.That(batch.TryTakeNext(0f, out _), Is.False);
        Assert.That(batch.TryTakeNext(float.NaN, out _), Is.False);
        Assert.That(batch.TryTakeNext(-1f, out _), Is.False);
        Assert.That(batch.TryTakeNext(1f, out _), Is.True);
        Assert.That(batch.TryTakeNext(1f, out _), Is.True);
        Assert.That(batch.TryTakeNext(1f, out _), Is.False);
        Assert.That(batch.RemainingCount, Is.Zero);
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidSpawnIntervalsFailCompilation(float interval)
    {
        var row = Rows("StateScript").Single(row => (int)row["Id"] == 30).ToObject<StateScriptData>();
        var spawn = row.Graphs.SelectMany(graph => graph.Nodes).OfType<SpawnUnitActionNodeData>().Single();
        spawn.SpawnIntervalSeconds = interval;
        Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { row }, out var registry, out string error), Is.False);
        if (registry.IsCreated) registry.Dispose();
        Assert.That(error, Does.Contain("SpawnIntervalSeconds"));
    }

    [TestCase("cleared")]
    [TestCase("dead")]
    [TestCase("destroying")]
    [TestCase("destroyed")]
    [TestCase("disabledGraph")]
    [TestCase("missingScript")]
    public void SpawnBatchStopsWhenItsOwnerOrGraphIsNoLongerValid(string reason)
    {
        using var f = new PatrolWorld();
        f.Manager.AddComponent<UnitStateScriptComponent>(f.Point);
        f.Manager.AddBuffer<StateScriptGraphStateElement>(f.Point).Add(new StateScriptGraphStateElement { IsActive = 1 });
        f.Manager.AddComponent<UnitDeathComponent>(f.Point);
        f.Manager.SetComponentEnabled<UnitDeathComponent>(f.Point, false);
        f.Manager.AddComponent<DestroyEntityFlag>(f.Point);
        f.Manager.SetComponentEnabled<DestroyEntityFlag>(f.Point, false);
        Assert.That(StateScriptSpawnBatch.CanContinue(f.Manager, f.Point, 0), Is.True);
        Assert.That(StateScriptSpawnBatch.CanContinue(f.Manager, f.Point, 1), Is.False);
        switch (reason)
        {
            case "cleared": f.Bool(f.Point, DungeonPatrolRuntimeUtility.EncounterDeadKey, true); break;
            case "dead": f.Manager.SetComponentEnabled<UnitDeathComponent>(f.Point, true); break;
            case "destroying": f.Manager.SetComponentEnabled<DestroyEntityFlag>(f.Point, true); break;
            case "destroyed": f.Manager.DestroyEntity(f.Point); break;
            case "disabledGraph":
                var graphStates = f.Manager.GetBuffer<StateScriptGraphStateElement>(f.Point);
                graphStates[0] = default;
                break;
            case "missingScript": f.Manager.RemoveComponent<UnitStateScriptComponent>(f.Point); break;
        }
        Assert.That(StateScriptSpawnBatch.CanContinue(f.Manager, f.Point, 0), Is.False);
    }

    [Test]
    public void ArrivalPropagatesInReverseOrderAndHoldsEachMembersOwnPosition()
    {
        using var f = new PatrolWorld();
        Entity last = f.Member(f.Point, 1.8f), middle = f.Member(f.Point, 0.95f), first = f.Member(f.Point, 0.1f);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(3));
        Assert.That(f.Vector(last).x, Is.EqualTo(1.8f));
        f.Position(middle, 1.2f);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(3), "No duplicate arrival reports.");
        Assert.That(f.Vector(middle).x, Is.EqualTo(0.95f), "Displacement must not move the saved stopping position.");
        Entity newcomer = f.Member(f.Point, 10f);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(3), "A late spawn must not reset existing arrivals.");
        Assert.That(f.Vector(first).x, Is.EqualTo(0.1f));
        Assert.That(f.Vector(newcomer).x, Is.Zero);
    }

    [Test]
    public void NextTargetVersionMakesTheWholeGroupLeaveTogether()
    {
        using var f = new PatrolWorld();
        Entity first = f.Member(f.Point, 0.1f), second = f.Member(f.Point, 0.95f);
        f.Tick();
        f.Position(f.Target, 10f);
        f.Number(f.Point, DungeonPatrolRuntimeUtility.PatrolTargetVersionKey, 2);
        f.Tick();
        Assert.That(f.Reached, Is.Zero);
        Assert.That(f.Vector(first).x, Is.EqualTo(10f));
        Assert.That(f.Vector(second).x, Is.EqualTo(10f));
    }

    [Test]
    public void ArrivalDoesNotTravelToTheBackOfAnArbitrarilyLongQueue()
    {
        using var f = new PatrolWorld();
        var members = Enumerable.Range(0, 12).Select(i => f.Member(f.Point, 0.1f + i * 0.85f)).ToArray();
        f.Tick();
        Assert.That(f.Reached, Is.InRange(2, 6));
        Assert.That(f.ReadNumber(members[^1], DungeonPatrolRuntimeUtility.PatrolReportedVersionKey, -1), Is.EqualTo(-1));
        Assert.That(f.Vector(members[^1]).x, Is.Zero);
    }

    [Test]
    public void GuardsOtherSquadsAndFightingMembersCannotPropagateArrival()
    {
        using var f = new PatrolWorld();
        f.Member(f.Point, 0.1f, false);
        Entity candidate = f.Member(f.Point, 0.95f);
        Entity otherPoint = f.AddPoint();
        f.Member(otherPoint, 0.1f);
        f.Tick();
        Assert.That(f.Reached, Is.Zero, "Neither guards nor the other squad can seed this squad.");
        Entity seed = f.Member(f.Point, 0.1f);
        f.Bool(seed, "dungeon.patrol.engaged", true);
        f.Tick();
        Assert.That(f.Reached, Is.Zero);
        f.Bool(seed, "dungeon.patrol.engaged", false);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(2));
        Assert.That(f.Vector(candidate).x, Is.EqualTo(0.95f));
    }

    [Test]
    public void DeadAndPendingMembersDoNotSeedArrivalAndCountsFollowSurvivors()
    {
        using var f = new PatrolWorld();
        Entity seed = f.Member(f.Point, 0.1f), follower = f.Member(f.Point, 0.95f);
        f.Manager.AddComponent<UnitInitializationPendingTag>(seed);
        f.Tick();
        Assert.That(f.Reached, Is.Zero);
        f.Manager.RemoveComponent<UnitInitializationPendingTag>(seed);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(2));
        f.Manager.SetComponentEnabled<UnitDeathComponent>(seed, true);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(1));
        f.Manager.SetComponentEnabled<DestroyEntityFlag>(follower, true);
        f.Tick();
        Assert.That(f.Reached, Is.Zero);
    }

    [Test]
    public void AThinWallBlocksArrivalPropagation()
    {
        using var f = new PatrolWorld();
        Entity mapEntity = f.Manager.CreateEntity();
        f.Manager.AddComponentData(mapEntity, new DungeonNavigationMapComponent
        {
            Width = 16, Height = 8, CellSize = 0.25f, WorldOrigin = new float2(-1),
        });
        var words = f.Manager.AddBuffer<DungeonNavigationCollisionWord>(mapEntity);
        words.Add(default);
        words.Add(new DungeonNavigationCollisionWord { Value = 1UL << 6 });
        f.Member(f.Point, 0.1f);
        f.Member(f.Point, 0.95f);
        f.Tick();
        Assert.That(f.Reached, Is.EqualTo(1));
    }

    [Test]
    public void MissingTargetOrClearedHomeDisablesPatrolDestinations()
    {
        using var f = new PatrolWorld();
        Entity member = f.Member(f.Point, 0.1f);
        f.Tick();
        f.Bool(f.Point, DungeonPatrolRuntimeUtility.EncounterDeadKey, true);
        f.Tick();
        Assert.That(f.ReadBool(member, DungeonPatrolRuntimeUtility.PatrolHasDestinationKey), Is.False);
        Assert.That(f.Reached, Is.Zero);
        f.Bool(f.Point, DungeonPatrolRuntimeUtility.EncounterDeadKey, false);
        f.Manager.DestroyEntity(f.Target);
        Assert.DoesNotThrow(f.Tick);
        Assert.That(f.ReadBool(member, DungeonPatrolRuntimeUtility.PatrolHasDestinationKey), Is.False);
    }

    [Test]
    public void IdleOrcaAgentStillParticipatesWhenItsMaximumSpeedIsNotZero()
    {
        var guard = new AgentData
        {
            Entity = new Entity { Index = 1, Version = 1 }, Position = new float2(1.2f, 0f),
            PreferredVelocity = float2.zero, Velocity = float2.zero, Radius = 0.4f,
            MaxSpeed = 3f, MaxNeighbors = 12, NeighborDistance = 4f, TimeHorizon = 0.8f,
        };
        var moving = guard;
        moving.Entity = new Entity { Index = 2, Version = 1 };
        moving.Position = float2.zero;
        moving.Velocity = new float2(3f, 0f);
        moving.PreferredVelocity = moving.Velocity;
        FixedList4096Bytes<AgentNeighbor> neighbors = default;
        float range = 16f;
        OrcaSolver.InsertNeighbor(in guard, in moving, ref neighbors, ref range);
        float2 corrected = OrcaSolver.ComputeNewVelocity(in guard, in neighbors, 1f / 60f);
        Assert.That(math.lengthsq(corrected), Is.GreaterThan(0.001f));
        Assert.That(math.all(math.isfinite(corrected)), Is.True);
    }

    private sealed class PatrolWorld : IDisposable
    {
        private readonly World _world = new("Patrol arrival test");
        private readonly SystemHandle _system;
        public EntityManager Manager => _world.EntityManager;
        public readonly Entity Target;
        public readonly Entity Point;
        public float Reached => ReadNumber(Point, DungeonPatrolRuntimeUtility.PatrolReachedCountKey);

        public PatrolWorld()
        {
            Target = Blackboard(0f);
            Point = AddPoint();
            _system = _world.GetOrCreateSystem<DungeonPatrolArrivalSystem>();
        }

        private Entity Blackboard(float x)
        {
            Entity entity = Manager.CreateEntity(typeof(UnitVariableComponent), typeof(LocalTransform));
            Manager.AddBuffer<UnitVariableElement>(entity);
            Manager.AddBuffer<UnitVariableConsumerElement>(entity);
            Position(entity, x);
            return entity;
        }

        public Entity AddPoint()
        {
            Entity point = Blackboard(0);
            Manager.AddComponentData(point, new DungeonInterestPointComponent { PatrolTarget = Target, ArrivalDistance = 0.75f });
            Bool(point, DungeonPatrolRuntimeUtility.PatrolActiveKey, true);
            Number(point, DungeonPatrolRuntimeUtility.PatrolTargetVersionKey, 1);
            return point;
        }

        public Entity Member(Entity owner, float x, bool patrol = true)
        {
            Entity member = Blackboard(x);
            UnitVariableSource.SetOther(Manager, member, owner);
            Manager.AddComponentData(member, new UnitNavigationComponent { ClearanceRadius = 0.35f });
            Manager.AddComponentData(member, new UnitAvoidanceComponent { RadiusPadding = 0.05f });
            Manager.AddComponent<UnitDeathComponent>(member);
            Manager.SetComponentEnabled<UnitDeathComponent>(member, false);
            Manager.AddComponent<DestroyEntityFlag>(member);
            Manager.SetComponentEnabled<DestroyEntityFlag>(member, false);
            Bool(member, DungeonPatrolRuntimeUtility.PatrolMemberKey, patrol);
            Bool(member, DungeonPatrolRuntimeUtility.GuardMemberKey, !patrol);
            return member;
        }

        public void Position(Entity entity, float x) => Manager.SetComponentData(entity, LocalTransform.FromPosition(x, 0, 0));
        public void Bool(Entity entity, string key, bool value) => UnitVariableSource.TrySetValue(Manager, entity, key, UnitValue.FromBool(value));
        public void Number(Entity entity, string key, float value) => UnitVariableSource.TrySetValue(Manager, entity, key, UnitValue.FromFloat(value));
        public bool ReadBool(Entity entity, string key) => UnitVariableSource.TryGetValue(Manager, entity, key, out var value) && value.TryGetBool(out bool result) && result;
        public float ReadNumber(Entity entity, string key, float fallback = 0) => UnitVariableSource.TryGetValue(Manager, entity, key, out var value) && value.TryGetNumber(out float number) ? number : fallback;
        public float3 Vector(Entity entity, string key = DungeonPatrolRuntimeUtility.PatrolDestinationKey)
        {
            Assert.That(UnitVariableSource.TryGetValue(Manager, entity, key, out var value), Is.True);
            Assert.That(value.TryGetFloat3(out float3 vector), Is.True);
            return vector;
        }
        public void Tick() { _system.Update(_world.Unmanaged); Manager.CompleteAllTrackedJobs(); }
        public void Dispose() => _world.Dispose();
    }
}
