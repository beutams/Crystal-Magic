using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public sealed class MonsterStartupRegressionTests
{
    private static JArray Rows(string name) => (JArray)JObject.Parse(File.ReadAllText(
        Path.Combine(Application.dataPath, "Res/Data/" + name + "DataTable.json")))["Rows"];
    private static JObject Row(string name, int id) => (JObject)Rows(name).Single(r => (int)r["Id"] == id);
    private static IEnumerable<int> CombatUnits() => Rows("BehaviorTree")
        .Select(r => (int)r["UnitDataId"]).Where(id => id != 3);

    [TestCaseSource(nameof(CombatUnits))]
    public void EveryMonsterInitializesCooldownsAndCompilesBothGraphs(int id)
    {
        using var f = new Fixture(id);
        f.Tick(3);
        var reads = Row("StateScript", id).Descendants().OfType<JObject>()
            .Concat(Row("BehaviorTree", id).Descendants().OfType<JObject>())
            .Where(o => (string)o["GetterKey"] == "unit.variables.getBool")
            .Select(o => (string)o["Inputs"]?[0]?["Literal"]?["String"])
            .Where(key => key != null && key.StartsWith("ai.") && key.EndsWith("coolingDown", StringComparison.OrdinalIgnoreCase))
            .Distinct();
        foreach (string key in reads)
        {
            Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Actor, key, out var value), Is.True, id + ": " + key);
            Assert.That(value.TryGetBool(out bool coolingDown), Is.True, key);
            Assert.That(coolingDown, Is.False, key);
        }
    }

    [TestCaseSource(nameof(CombatUnits))]
    public void EveryRequestedAnimationHasExistingLeftAndRightClips(int id)
    {
        JToken profile = Rows("UnitAnimationProfile").Single(p => (int)p["UnitDataId"] == id);
        string library = File.ReadAllText(Path.Combine(Application.dataPath, "Res/Data/UnitAnimationFrameLibrary.asset"));
        foreach (JToken node in Row("StateScript", id)["Graphs"].SelectMany(g => g["Nodes"]))
        {
            if ((string)node["SetterKey"] is not ("unit.animation.play" or "unit.animation.setName")) continue;
            string name = (string)node["Value"]?["Literal"]?["String"];
            if (name == null) continue;
            JToken animation = profile["Animations"].SingleOrDefault(a => (string)a["Name"] == name);
            Assert.That(animation, Is.Not.Null, id + ": " + (string)node["Guid"] + " -> " + name);
            foreach (string side in new[] { "Left", "Right" })
            {
                string clip = (string)animation[side + "ClipPath"];
                string full = Path.Combine(Application.dataPath, "..", clip);
                Assert.That(File.Exists(full), Is.True, clip);
                Assert.That(library, Does.Contain(clip), "Frame library missing " + clip);
                Assert.That(Regex.Matches(File.ReadAllText(full), @"value: \{fileID: -?\d+, guid:").Count,
                    Is.GreaterThan(1), "Animation must contain multiple sprite frames: " + clip);
            }
        }
    }

    [TestCase(9, "Attack01", 46)]
    [TestCase(9, "Attack02", 47)]
    [TestCase(10, "Attack01", 43)]
    [TestCase(10, "Attack02", 44)]
    [TestCase(10, "Attack03", 45)]
    [TestCase(16, "Attack01", 40)]
    [TestCase(16, "Attack02", 41)]
    [TestCase(28, "Attack01", 19)]
    [TestCase(28, "Attack02", 42)]
    public void FreshMonsterCanReleaseAttackAndUnlockAfterRecovery(int id, string intent, int expectedSkill)
    {
        using var f = new Fixture(id);
        f.TargetAt(new float3(1, 0, 0));
        f.Tick(3);
        f.Set("ai.intent", UnitValue.FromString(intent));
        for (int i = 0; i < 120 && !f.Skills.Contains(expectedSkill); i++)
            f.Tick(1);
        f.Set("ai.intent", UnitValue.FromString("Idle"));
        f.Tick(120);
        Assert.That(f.Skills.Count, Is.GreaterThan(0), id + " " + intent + " never released");
        int expectedHits = intent == "Attack02" ? id == 9 ? 4 : id == 10 ? 2 : 1 : 1;
        Assert.That(f.Skills.Count(skill => skill == expectedSkill), Is.EqualTo(expectedHits), "Missing or repeated attack hits");
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Actor, "ai.attack.locked", out var locked), Is.True);
        Assert.That(locked.Bool, Is.Zero, "Attack never recovered");
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).StateMoveMultiplier, Is.EqualTo(1));
    }

    [TestCase(9)]
    [TestCase(10)]
    [TestCase(28)]
    public void BehaviorTreeActuallyStartsNearbyAttackWithoutManuallySettingIntent(int id)
    {
        using var f = new Fixture(id);
        JToken hit = Row("BehaviorTree", id)["Nodes"].First(n => (string)n["Type"] == "HitCheck");
        f.TargetAt(new float3((float)hit["Center"]["x"] * 6, (float)hit["Center"]["y"] * 6, 0));
        f.Tick(3);
        f.BehaviorTick();
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Actor, "ai.intent", out var intent), Is.True);
        Assert.That(intent.String.ToString(), Does.StartWith("Attack"));
        f.Tick(90);
        Assert.That(f.Skills.Count, Is.GreaterThan(0), "Behavior selected attack but no skill was emitted");
    }

    [TestCase(9, "Move")]
    [TestCase(10, "Move")]
    [TestCase(28, "Walk")]
    public void DistantPlayerStartsChaseWithValidMovingAnimationWhenAttacksAreUnavailable(int id, string animation)
    {
        using var f = new Fixture(id);
        f.TargetAt(new float3(9, 0, 0));
        f.Tick(3);
        // Werewolf can teleport across its perception range; chase is its cooldown fallback.
        f.Set("ai.skill.coolingDown", UnitValue.FromBool(true));
        f.BehaviorTick();
        f.Tick(2);
        Assert.That(f.Manager.GetComponentData<UnitNavigationComponent>(f.Actor).HasDestination, Is.EqualTo(1));
        Assert.That(f.Manager.GetComponentData<UnitAnimationComponent>(f.Actor).AnimationName.ToString(), Is.EqualTo(animation));
    }

    [Test]
    public void BatStartsFlyingAndMovingWithoutNeedingItsFirstAttack()
    {
        using var f = new Fixture(16);
        f.Tick(3);
        f.BehaviorTick();
        f.Tick(2);
        var move = f.Manager.GetComponentData<UnitMoveComponent>(f.Actor);
        Assert.That(math.lengthsq(move.Direction), Is.GreaterThan(.1f));
        Assert.That(move.CommandMoveSpeed, Is.EqualTo(3.5f));
        Assert.That(move.StateMoveMultiplier, Is.EqualTo(1));
        Assert.That(f.Manager.GetComponentData<UnitAnimationComponent>(f.Actor).AnimationName.ToString(), Is.EqualTo("Flying"));
        f.World.GetOrCreateSystem<UnitMoveSystem>().Update(f.World.Unmanaged);
        f.Manager.CompleteAllTrackedJobs();
        Assert.That(math.lengthsq(f.Manager.GetComponentData<PhysicsVelocity>(f.Actor).Linear), Is.GreaterThan(.1f));
    }

    [TestCase(15, "Attack02", .5f)]
    [TestCase(24, "Attack03", 1f)]
    public void PreviouslyConfiguredMovingAttacksKeepTheirSpeed(int id, string intent, float speed)
    {
        using var f = new Fixture(id);
        f.TargetAt(new float3(4, 0, 0));
        f.Tick(3);
        f.Set("ai.intent", UnitValue.FromString(intent));
        f.Tick(2);
        f.BehaviorTick();
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).StateMoveMultiplier, Is.EqualTo(speed));
        Assert.That(f.Manager.GetComponentData<UnitNavigationComponent>(f.Actor).HasDestination, Is.EqualTo(1));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly World World = new("Monster startup regression");
        public EntityManager Manager => World.EntityManager;
        public Entity Actor { get; }
        public readonly List<int> Skills = new();
        private BlobAssetReference<StateScriptRuntimeRegistryBlob> scripts;
        private BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> trees;
        private readonly StateScriptSystem stateSystem;
        private readonly MonsterStartupBehaviorDriverSystem behaviorSystem;
        private double elapsed;

        public Fixture(int id)
        {
            GameSingletonUtility.Create(Manager, GameWorldRole.Standalone, GameSceneMode.Dungeon);
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
            Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { Row("StateScript", id).ToObject<StateScriptData>(serializer) }, out scripts, out string error), Is.True, error);
            Assert.That(BehaviorTreeCompiler.TryBuildRegistry(new[] { Row("BehaviorTree", id).ToObject<BehaviorTreeData>(serializer) }, out trees, out error), Is.True, error);
            Manager.SetComponentData(Manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent)), new StateScriptRuntimeRegistryComponent { Value = scripts });
            Entity queryTree = Manager.CreateEntity();
            Manager.AddBuffer<UnitQueryNode>(queryTree).EnsureCapacity(1);
            Manager.AddBuffer<UnitQueryEntry>(queryTree).EnsureCapacity(1);
            Manager.SetComponentData(Manager.CreateEntity(typeof(UnitQuerySingleton)), new UnitQuerySingleton { TreeEntity = queryTree });
            Actor = Manager.CreateEntity(typeof(LocalTransform), typeof(UnitVariableComponent), typeof(UnitAnimationComponent),
                typeof(UnitMoveComponent), typeof(UnitNavigationComponent), typeof(UnitFacingComponent), typeof(UnitPerceptionComponent),
                typeof(PhysicsVelocity), typeof(UnitFactionComponent), typeof(UnitControlRuntimeComponent));
            Manager.SetComponentData(Actor, new UnitFactionComponent { Value = UnitFactionType.Enemy });
            Manager.SetComponentData(Actor, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 6));
            Manager.SetComponentData(Actor, new UnitMoveComponent { BaseMoveSpeed = 6, BaseMaxAcceleration = 10000, StateMoveMultiplier = 1, CommandMoveSpeed = -1 });
            Manager.SetComponentData(Actor, new UnitFacingComponent { Direction = new float2(1, 0) });
            Manager.AddBuffer<UnitVariableElement>(Actor);
            Manager.AddBuffer<UnitVariableConsumerElement>(Actor);
            Manager.AddBuffer<UnitPerceptionUnitElement>(Actor);
            Manager.AddBuffer<UnitNavigationPathElement>(Actor);
            Manager.AddComponentData(Actor, new UnitStateScriptComponent { UnitDataId = id, DefinitionIndex = 0 });
            var graphs = Manager.AddBuffer<StateScriptGraphStateElement>(Actor);
            int start = 0;
            for (int i = 0; i < scripts.Value.Units[0].Graphs.Length; i++)
            {
                graphs.Add(new StateScriptGraphStateElement { NodeStateStart = start });
                start += scripts.Value.Units[0].Graphs[i].Nodes.Length;
            }
            var nodeStates = Manager.AddBuffer<StateScriptNodeStateElement>(Actor);
            for (int i = 0; i < start; i++) nodeStates.Add(default);
            Manager.AddBuffer<StateScriptSourceCommandElement>(Actor);
            Manager.AddBuffer<StateScriptSourceCommandArgumentElement>(Actor);
            Manager.AddBuffer<StateScriptExternalResultElement>(Actor);
            Manager.AddComponentData(Actor, new UnitBehaviorTreeComponent { UnitDataId = id, TreeIndex = 0, CurrentNodeIndex = -1 });
            var behaviorStates = Manager.AddBuffer<BehaviorNodeStateElement>(Actor);
            for (int i = 0; i < trees.Value.Trees[0].Nodes.Length; i++) behaviorStates.Add(default);
            Manager.AddBuffer<BehaviorTreeCommandElement>(Actor);
            Manager.AddBuffer<BehaviorTreeCommandArgumentElement>(Actor);
            Manager.AddBuffer<BehaviorTreeMoveCommandElement>(Actor);
            Manager.AddBuffer<BehaviorTreeHitDebugElement>(Actor);
            World.GetOrCreateSystemManaged<UnitSourceDispatcherSystem>();
            stateSystem = World.GetOrCreateSystemManaged<StateScriptSystem>();
            behaviorSystem = World.GetOrCreateSystemManaged<MonsterStartupBehaviorDriverSystem>();
            behaviorSystem.Registry = trees;
        }

        public void Set(string key, UnitValue value) => UnitVariableSource.TrySetValue(Manager, Actor, key, value);

        public void TargetAt(float3 position)
        {
            Entity target = Manager.CreateEntity(typeof(LocalTransform), typeof(UnitFactionComponent));
            Manager.SetComponentData(target, LocalTransform.FromPosition(position));
            Manager.SetComponentData(target, new UnitFactionComponent { Value = UnitFactionType.Player });
            Manager.GetBuffer<UnitPerceptionUnitElement>(Actor).Add(new UnitPerceptionUnitElement { Value = target, Faction = UnitFactionType.Player, DistanceSq = math.lengthsq(position) });
        }

        public void BehaviorTick()
        {
            behaviorSystem.Update();
            Manager.CompleteAllTrackedJobs();
        }

        public void Tick(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                elapsed += 1.0 / 60;
                World.SetTime(new TimeData(elapsed, 1f / 60));
                stateSystem.Update();
                Manager.CompleteAllTrackedJobs();
                Entity queue = StateScriptManagedCommandQueueUtility.GetOrCreateEntity(Manager);
                var commands = Manager.GetBuffer<StateScriptManagedCommandElement>(queue);
                foreach (var command in commands)
                    if (command.Type == StateScriptManagedCommandType.RequestSkill) Skills.Add(command.IntValue);
                commands.Clear();
            }
        }

        public void Dispose()
        {
            World.Dispose();
            scripts.Dispose();
            trees.Dispose();
        }
    }
}
