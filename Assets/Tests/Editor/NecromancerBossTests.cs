using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public sealed class NecromancerBossTests
{
    private static JArray Rows(string table) => (JArray)JObject.Parse(File.ReadAllText(
        Path.Combine(Application.dataPath, $"Res/Data/{table}DataTable.json")))["Rows"];

    [Test]
    public void AllAuthoredGraphsCompile()
    {
        Assert.That(StateScriptCompiler.TryBuildRegistry(Rows("StateScript").Select(r => r.ToObject<StateScriptData>()).ToArray(),
            out var scripts, out string error), Is.True, error);
        scripts.Dispose();
        Assert.That(BehaviorTreeCompiler.TryBuildRegistry(Rows("BehaviorTree").Select(r => r.ToObject<BehaviorTreeData>()).ToArray(),
            out var trees, out error), Is.True, error);
        trees.Dispose();
    }

    [Test]
    public void CircleSamplingIsUniformByAreaAndHonorsMinimumRadius()
    {
        var random = Unity.Mathematics.Random.CreateFromIndex(17);
        float sum = 0;
        for (int i = 0; i < 10000; i++)
        {
            float3 point = SpawnPositionUtility.Sample(new float3(5, 6, 2), 2, 4, ref random);
            float squared = math.distancesq(point.xy, new float2(5, 6));
            Assert.That(squared, Is.InRange(3.9999f, 16.0001f));
            Assert.That(point.z, Is.EqualTo(2));
            sum += squared;
        }
        Assert.That(sum / 10000, Is.EqualTo(10).Within(0.2f));
    }

    [Test]
    public void ValidationRejectsWallsBoundsAndMissingMapsWithoutFallingBackIntoWalls()
    {
        using var world = new World("Spawn position validation");
        EntityManager manager = world.EntityManager;
        var random = Unity.Mathematics.Random.CreateFromIndex(1);
        Assert.That(SpawnPositionUtility.TrySample(manager, new float3(3.5f), 0, 0, true, 0, 4, ref random, out _), Is.False);
        Assert.That(SpawnPositionUtility.TrySample(manager, new float3(3.5f), 0, 0, false, 0, 4, ref random, out _), Is.True);
        Entity mapEntity = AddMap(manager, 8);
        var words = manager.GetBuffer<DungeonNavigationCollisionWord>(mapEntity);
        words[0] = new DungeonNavigationCollisionWord { Value = 1UL << (3 * 8 + 3) };
        Assert.That(SpawnPositionUtility.IsValid(manager, new float3(3.5f, 3.5f, 0), 0), Is.False);
        Assert.That(SpawnPositionUtility.IsValid(manager, new float3(2.9f, 3.5f, 0), 0.2f), Is.False);
        Assert.That(SpawnPositionUtility.IsValid(manager, new float3(2.5f, 3.5f, 0), 0.2f), Is.True);
        Assert.That(SpawnPositionUtility.IsValid(manager, new float3(-1, 2, 0), 0), Is.False);
        Assert.That(SpawnPositionUtility.IsValid(manager, new float3(8, 2, 0), 0), Is.False);
        words = manager.GetBuffer<DungeonNavigationCollisionWord>(mapEntity);
        words[0] = new DungeonNavigationCollisionWord { Value = ulong.MaxValue };
        Assert.That(SpawnPositionUtility.TrySample(manager, new float3(4, 4, 0), 0, 3, true, 0.5f, 128, ref random, out _), Is.False);
    }

    [Test]
    public void AuthoredBarrageCapturesSixIndependentFixedPositions()
    {
        var serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        SkillData skill = Rows("Skill").Single(r => (int)r["Id"] == 70).ToObject<SkillData>(serializer);
        var data = (PersistentEffectData)skill.EffectChain.Single();
        Assert.That(data.PlacementCount, Is.EqualTo(6));
        Assert.That(data.PlacementRadius, Is.EqualTo(4));
        Assert.That(data.ValidatePlacementPosition, Is.True);
        Assert.That(data.OnTickEffects, Is.Empty);
        Assert.That(data.TotalDuration, Is.EqualTo(0.25f));
        var search = (AreaSearchEffectData)data.OnEndEffects.Single();
        Assert.That(search.Radius, Is.EqualTo(1.2f));
        Assert.That(((DamageEffectData)search.OnAfterSearch.Single()).DamageCoefficient, Is.EqualTo(2.5f));
        using var world = new World("Necromancer barrage snapshot");
        EntityManager manager = world.EntityManager;
        AddMap(manager, 32);
        var context = new SkillContent { EntityManager = manager, HasPosition = true, Position = new Vector3(16, 16, 0) };
        new PersistentEffect(data).Execute(context);
        context.Position = new Vector3(25, 25, 0);
        Entity queue = PersistentEffectUtility.GetOrCreateEntity(manager);
        var requests = manager.GetBuffer<PersistentEffectRequest>(queue);
        Assert.That(requests.Length, Is.EqualTo(6));
        for (int i = 0; i < requests.Length; i++)
            Assert.That(math.distance(requests[i].ReleasePosition.xy, new float2(16)), Is.LessThanOrEqualTo(4.0001f));
        Assert.That(math.distancesq(requests[0].ReleasePosition, requests[1].ReleasePosition), Is.GreaterThan(0));
        EffectDataBridgeUtility.ClearTransient(manager);
    }

    [Test]
    public void BossTemplateHasOneGuaranteedBossEvenWithLargeBudgetGrowth()
    {
        JToken pools = Rows("DungeonTheme").Single(r => (int)r["Id"] == 0)["OpenField"]["EncounterPools"];
        JToken large = pools.Single(p => (int)p["InterestSize"] == 2);
        var boss = large["Squads"].Single(s => (string)s["Name"] == "Necromancer Boss").ToObject<OpenFieldDungeonSquadData>();
        Assert.That(boss.IsBossSquad, Is.True);
        boss.EnsureValid();
        Assert.That(boss.CostLimit, Is.EqualTo(25));
        var leader = boss.Members.Single(m => m.UnitName == "Necromancer");
        Assert.That(leader.MinCount, Is.EqualTo(1));
        Assert.That(leader.Cost, Is.EqualTo(10));
        Assert.That(leader.Weight, Is.Zero);
        using var choices = new NativeArray<UnitRosterUtility.Choice>(boss.Members.Select(member =>
            new UnitRosterUtility.Choice
            {
                Unit = new FixedString128Bytes(member.UnitName), MinCount = member.MinCount,
                Cost = member.Cost, Weight = member.Weight,
            }).ToArray(), Allocator.Temp);
        int bossIndex = boss.Members.IndexOf(leader);
        int largestRandomCost = boss.Members.Where(m => m.Weight > 0).Max(m => m.Cost);
        NativeList<int> result = new(Allocator.Temp);
        try
        {
            foreach (float multiplier in new[] { 1f, 1.45f, 100f })
            {
                var random = Unity.Mathematics.Random.CreateFromIndex(42);
                Assert.That(UnitRosterUtility.Build(choices, boss.CostLimit, multiplier, ref random, ref result), Is.True);
                int bossCount = 0;
                int totalCost = 0;
                foreach (int index in result)
                {
                    if (index == bossIndex) bossCount++;
                    totalCost += choices[index].Cost;
                }
                Assert.That(bossCount, Is.EqualTo(1));
                Assert.That(result.Length, Is.GreaterThan(1));
                Assert.That(totalCost, Is.GreaterThanOrEqualTo(boss.CostLimit * multiplier));
                Assert.That(totalCost, Is.LessThan(boss.CostLimit * multiplier + largestRandomCost));
            }
        }
        finally { result.Dispose(); }
        foreach (JToken pool in pools.Where(p => (int)p["InterestSize"] != 2))
            Assert.That(pool["Squads"].Any(s => (string)s["Name"] == boss.Name), Is.False);
    }

    [Test]
    public void BossSupportSquadsUseLargeBudgetsAndNeverIncludeTheBoss()
    {
        var theme = Rows("DungeonTheme").Single(r => (int)r["Id"] == 0).ToObject<DungeonThemeData>();
        var large = theme.OpenField.EncounterPools.Single(p => p.InterestSize == OpenFieldInterestSizeData.Large);
        var boss = large.Squads.Single(s => s.Name == "Necromancer Boss");
        foreach (var normal in large.Squads.Where(s => !s.IsBossSquad))
        {
            Assert.That(boss.CostLimit, Is.EqualTo(normal.CostLimit));
            Assert.That(boss.Patrol.CostLimit, Is.EqualTo(normal.Patrol.CostLimit));
            Assert.That(boss.Revenge.CostLimit, Is.EqualTo(normal.Revenge.CostLimit));
        }
        foreach (var support in new[] { boss.Patrol, boss.Revenge })
        {
            Assert.That(support.Members.Any(m => m.UnitName == "Necromancer"), Is.False);
            Assert.That(support.Members.Where(m => m.UnitName == "Skeleton Archer").All(m => m.Cost == 1), Is.True);
            Assert.That(support.Members.Sum(m => m.MinCount * m.Cost), Is.LessThanOrEqualTo(support.CostLimit));
        }
        foreach (var squad in theme.OpenField.EncounterPools.SelectMany(p => p.Squads)
                     .Concat(theme.OpenField.WildSquads).Where(s => s != boss))
            Assert.That(squad.Members.Any(m => m.UnitName == "Necromancer"), Is.False);
    }

    [TestCase(1, 0, true)]
    [TestCase(0, 1, true)]
    [TestCase(0, 0, false)]
    public void ExitValidationAcceptsGuaranteedOrRandomMembers(int minimum, int weight, bool expected)
    {
        var theme = Rows("DungeonTheme").Single(r => (int)r["Id"] == 0).ToObject<DungeonThemeData>();
        var boss = theme.OpenField.EncounterPools.SelectMany(p => p.Squads).Single(s => s.IsBossSquad);
        boss.Members.RemoveAll(m => m.UnitName != "Necromancer");
        boss.Members[0].MinCount = minimum;
        boss.Members[0].Weight = weight;
        MethodInfo validate = typeof(DungeonMapPlanBuilder).GetMethod("HasConfiguredExitSquad",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(validate, Is.Not.Null);
        Assert.That((bool)validate.Invoke(null, new object[] { theme.OpenField, true }), Is.EqualTo(expected));
    }

    [TestCase(15)]
    [TestCase(18)]
    [TestCase(26)]
    [TestCase(27)]
    public void SummonedSkeletonPlaysArrivalBeforeNormalGraphs(int id)
    {
        using var f = new ScriptWorld(id);
        UnitVariableSource.TrySetValue(f.Manager, f.Actor, "boss.summoned", UnitValue.FromBool(true));
        f.Tick(2);
        Assert.That(f.Manager.GetComponentData<UnitAnimationComponent>(f.Actor).AnimationName.ToString(), Is.EqualTo("Summon"));
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).StateMoveMultiplier, Is.Zero);
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Actor, "boss.summon.ready", out _), Is.False);
        f.Tick(40);
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Actor, "boss.summon.ready", out var ready), Is.True);
        Assert.That(ready.Bool, Is.EqualTo(1));
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).StateMoveMultiplier, Is.EqualTo(1));
    }

    [TestCase(0, 3)]
    [TestCase(6, 2)]
    [TestCase(7, 1)]
    [TestCase(8, 0)]
    public void SummonReleaseNeverQueuesMoreThanRemainingSlots(int living, int expected)
    {
        using var f = new ScriptWorld(22);
        f.Tick(2);
        Entity player = f.Manager.CreateEntity(typeof(LocalTransform));
        f.Manager.SetComponentData(player, LocalTransform.FromPosition(new float3(8, 0, 0)));
        f.Manager.GetBuffer<UnitPerceptionUnitElement>(f.Actor).Add(new UnitPerceptionUnitElement
            { Value = player, Faction = UnitFactionType.Player, DistanceSq = 64 });
        for (int i = 0; i < living; i++)
        {
            Entity member = f.Manager.CreateEntity(typeof(UnitVariableComponent));
            f.Manager.AddBuffer<UnitVariableElement>(member);
            UnitVariableSource.SetOther(f.Manager, member, f.Actor);
            UnitVariableSource.TrySetValue(f.Manager, member, "boss.summoned", UnitValue.FromBool(true));
        }
        UnitVariableSource.TrySetValue(f.Manager, f.Actor, "ai.intent", UnitValue.FromString("Summon"));
        f.Tick(40);
        Entity queue = StateScriptManagedCommandQueueUtility.GetOrCreateEntity(f.Manager);
        int count = 0;
        foreach (var command in f.Manager.GetBuffer<StateScriptManagedCommandElement>(queue))
            if (command.Type == StateScriptManagedCommandType.SpawnUnit)
                count += f.Registry.Value.Units[0].Graphs[command.GraphIndex].Nodes[command.NodeIndex].IntParameters.x;
        Assert.That(count, Is.EqualTo(expected));
    }

    [TestCase("Necromancer_Attack02_Effect", 6)]
    [TestCase("Necromancer_Sumon_Effect", 7)]
    public void EffectAssetsHaveAllSpriteFrames(string name, int frames)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Res/Prefab/VFX/{name}.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        AnimationClip clip = prefab.GetComponent<SpriteEffectAnimationAuthoring>().EnterClip;
        Assert.That(clip, Is.Not.Null);
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
        Assert.That(keys.Length, Is.EqualTo(frames));
        Assert.That(keys.All(k => k.value is Sprite), Is.True);
    }

    [TestCase("Left")]
    [TestCase("Right")]
    public void BlinkArrivalIsTheExactReverseOfDeathWithoutChangingDeath(string side)
    {
        string folder = "Assets/Res/Animation/Necromancer/";
        var death = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Death" + side + ".anim");
        var arrival = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "TeleportIn" + side + ".anim");
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var forward = AnimationUtility.GetObjectReferenceCurve(death, binding);
        var reverse = AnimationUtility.GetObjectReferenceCurve(arrival, binding);
        Assert.That(reverse.Length, Is.EqualTo(9));
        CollectionAssert.AreEqual(forward.Select(k => k.value).Reverse(), reverse.Select(k => k.value));
        CollectionAssert.AreEqual(forward.Select(k => k.time), reverse.Select(k => k.time));
        Assert.That(arrival.length, Is.EqualTo(death.length).Within(0.0001));
        var profile = Rows("UnitAnimationProfile").Single(r => (int)r["UnitDataId"] == 22);
        var entries = profile["Animations"];
        Assert.That(entries.Single(a => (string)a["Name"] == "Death")[side + "ClipPath"].Value<string>(),
            Is.EqualTo(entries.Single(a => (string)a["Name"] == "TeleportOut")[side + "ClipPath"].Value<string>()));
    }

    [Test]
    public void BlinkFindsNearbyClearGroundAndNeverFallsBackIntoWalls()
    {
        using var world = new World("Blink position validation");
        EntityManager manager = world.EntityManager;
        Entity mapEntity = AddMap(manager, 8);
        var map = manager.GetComponentData<DungeonNavigationMapComponent>(mapEntity);
        var words = manager.GetBuffer<DungeonNavigationCollisionWord>(mapEntity).AsNativeArray();
        float3 target = new(3.5f, 3.5f, 0);
        Assert.That(SpawnPositionUtility.TryFindNearby(in map, words, target, 0.6f, 2, out var point), Is.True);
        Assert.That(point, Is.EqualTo(target));
        words[0] = new DungeonNavigationCollisionWord { Value = 1UL << (3 * 8 + 3) };
        Assert.That(SpawnPositionUtility.TryFindNearby(in map, words, target, 0.6f, 2, out point), Is.True);
        Assert.That(math.distance(point, target), Is.LessThanOrEqualTo(2.0001f));
        Assert.That(SpawnPositionUtility.CanOccupy(in map, words, point.xy, 0.6f), Is.True);
        words[0] = new DungeonNavigationCollisionWord { Value = ulong.MaxValue };
        Assert.That(SpawnPositionUtility.TryFindNearby(in map, words, target, 0.6f, 2, out _), Is.False);
    }

    [Test]
    public void BlinkDisappearsThenTeleportsAwayFromLatestPlayerPositionThenReappears()
    {
        using var f = new ScriptWorld(22);
        AddMap(f.Manager, 32);
        f.Manager.SetComponentData(f.Actor, LocalTransform.FromPosition(new float3(16, 16, 0)));
        f.Manager.AddComponent<PhysicsVelocity>(f.Actor);
        f.Tick(2);
        Entity player = f.Manager.CreateEntity(typeof(LocalTransform));
        f.Manager.SetComponentData(player, LocalTransform.FromPosition(new float3(19, 16, 0)));
        f.Manager.GetBuffer<UnitPerceptionUnitElement>(f.Actor).Add(new UnitPerceptionUnitElement
            { Value = player, Faction = UnitFactionType.Player, DistanceSq = 9 });
        UnitVariableSource.TrySetValue(f.Manager, f.Actor, "ai.intent", UnitValue.FromString("Blink"));
        f.Tick(2);
        Assert.That(f.Manager.GetComponentData<UnitAnimationComponent>(f.Actor).AnimationName.ToString(), Is.EqualTo("TeleportOut"));
        f.Tick(30);
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).HasTeleportRequest, Is.Zero);
        f.Manager.SetComponentData(player, LocalTransform.FromPosition(new float3(16, 19, 0)));
        f.Tick(20);
        var move = f.Manager.GetComponentData<UnitMoveComponent>(f.Actor);
        Assert.That(move.HasTeleportRequest, Is.EqualTo(1));
        Assert.That(move.ValidateTeleportPosition, Is.EqualTo(1));
        Assert.That(math.distance(move.TeleportDestination, new float3(16, 10, 0)), Is.LessThan(0.0001f));
        Assert.That(f.Manager.GetComponentData<UnitAnimationComponent>(f.Actor).AnimationName.ToString(), Is.EqualTo("TeleportIn"));
        Assert.That(move.StateMoveMultiplier, Is.Zero);
        f.TickMovement();
        Assert.That(math.distance(f.Manager.GetComponentData<LocalTransform>(f.Actor).Position, new float3(16, 10, 0)), Is.LessThan(0.0001f));
        f.Tick(50);
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).StateMoveMultiplier, Is.EqualTo(1));
        Assert.That(UnitVariableSource.TryGetValue(f.Manager, f.Actor, "ai.blink.coolingDown", out var coolingDown), Is.True);
        Assert.That(coolingDown.Bool, Is.EqualTo(1));
        UnitVariableSource.TrySetValue(f.Manager, f.Actor, "ai.intent", UnitValue.FromString("Blink"));
        f.Tick(2);
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).HasTeleportRequest, Is.Zero);
        UnitVariableSource.TrySetValue(f.Manager, f.Actor, "ai.intent", UnitValue.FromString("Idle"));
        f.Tick(280);
        UnitVariableSource.TryGetValue(f.Manager, f.Actor, "ai.blink.coolingDown", out coolingDown);
        Assert.That(coolingDown.Bool, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ValidatedTeleportWithNoLegalPointStaysPutAndConsumesRequest(bool blockedMap)
    {
        using var f = new ScriptWorld(22);
        f.Manager.AddComponent<PhysicsVelocity>(f.Actor);
        float3 original = new(3, 3, 0);
        f.Manager.SetComponentData(f.Actor, LocalTransform.FromPosition(original));
        if (blockedMap)
        {
            Entity map = AddMap(f.Manager, 8);
            var words = f.Manager.GetBuffer<DungeonNavigationCollisionWord>(map);
            words[0] = new DungeonNavigationCollisionWord { Value = ulong.MaxValue };
        }
        f.Manager.SetComponentData(f.Actor, new UnitMoveComponent
        {
            HasTeleportRequest = 1, ValidateTeleportPosition = 1,
            TeleportDestination = new float3(5, 5, 0), TeleportClearanceRadius = 0.6f, TeleportSearchRadius = 2,
        });
        f.TickMovement();
        Assert.That(f.Manager.GetComponentData<LocalTransform>(f.Actor).Position, Is.EqualTo(original));
        Assert.That(f.Manager.GetComponentData<UnitMoveComponent>(f.Actor).HasTeleportRequest, Is.Zero);
    }

    [Test]
    public void BlinkHasPriorityOverMeleeButDoesNotInterruptBusySkills()
    {
        JToken tree = Rows("BehaviorTree").Single(r => (int)r["Id"] == 22);
        string[] priority = tree["Nodes"].Single(n => (string)n["Guid"] == "necro_selector")["ChildGuids"].Values<string>().ToArray();
        Assert.That(priority.Take(3), Is.EqualTo(new[] { "necro_busy", "necro_blink", "necro_melee" }));
        JToken check = tree["Nodes"].Single(n => (string)n["Guid"] == "necro_blink_check");
        Assert.That((float)check["Conditions"][1]["Inputs"][1]["Literal"]["Float"], Is.EqualTo(4));
    }

    private static Entity AddMap(EntityManager manager, int size)
    {
        Entity map = manager.CreateEntity(typeof(DungeonNavigationMapComponent));
        manager.SetComponentData(map, new DungeonNavigationMapComponent { Width = size, Height = size, CellSize = 1 });
        var words = manager.AddBuffer<DungeonNavigationCollisionWord>(map);
        for (int i = 0; i < DungeonNavigationMapUtility.GetRequiredWordCount(size * size); i++) words.Add(default);
        return map;
    }

    private sealed class ScriptWorld : IDisposable
    {
        private readonly World world = new("Necromancer state test");
        public BlobAssetReference<StateScriptRuntimeRegistryBlob> Registry;
        private readonly StateScriptSystem system;
        private double elapsed;
        public EntityManager Manager => world.EntityManager;
        public Entity Actor { get; }

        public ScriptWorld(int id)
        {
            GameSingletonUtility.Create(Manager, GameWorldRole.Standalone, GameSceneMode.Dungeon);
            var script = Rows("StateScript").Single(r => (int)r["Id"] == id).ToObject<StateScriptData>();
            Assert.That(StateScriptCompiler.TryBuildRegistry(new[] { script }, out Registry, out string error), Is.True, error);
            Entity registry = Manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent));
            Manager.SetComponentData(registry, new StateScriptRuntimeRegistryComponent { Value = Registry });
            Entity tree = Manager.CreateEntity();
            Manager.AddBuffer<UnitQueryNode>(tree).EnsureCapacity(1);
            Manager.AddBuffer<UnitQueryEntry>(tree).EnsureCapacity(1);
            Entity query = Manager.CreateEntity(typeof(UnitQuerySingleton));
            Manager.SetComponentData(query, new UnitQuerySingleton { TreeEntity = tree });
            Actor = Manager.CreateEntity(typeof(LocalTransform), typeof(UnitVariableComponent), typeof(UnitAnimationComponent),
                typeof(UnitMoveComponent), typeof(UnitNavigationComponent), typeof(UnitFacingComponent), typeof(UnitPerceptionComponent));
            Manager.SetComponentData(Actor, LocalTransform.Identity);
            Manager.AddBuffer<UnitVariableElement>(Actor);
            Manager.AddBuffer<UnitVariableConsumerElement>(Actor);
            Manager.AddBuffer<UnitPerceptionUnitElement>(Actor);
            Manager.AddComponentData(Actor, new UnitStateScriptComponent { UnitDataId = id, DefinitionIndex = 0 });
            var graphs = Manager.AddBuffer<StateScriptGraphStateElement>(Actor);
            int start = 0;
            for (int i = 0; i < Registry.Value.Units[0].Graphs.Length; i++)
            {
                graphs.Add(new StateScriptGraphStateElement { NodeStateStart = start });
                start += Registry.Value.Units[0].Graphs[i].Nodes.Length;
            }
            Manager.AddBuffer<StateScriptNodeStateElement>(Actor).ResizeUninitialized(start);
            var states = Manager.GetBuffer<StateScriptNodeStateElement>(Actor);
            for (int i = 0; i < states.Length; i++) states[i] = default;
            Manager.AddBuffer<StateScriptSourceCommandElement>(Actor);
            Manager.AddBuffer<StateScriptSourceCommandArgumentElement>(Actor);
            Manager.AddBuffer<StateScriptExternalResultElement>(Actor);
            system = world.GetOrCreateSystemManaged<StateScriptSystem>();
        }

        public void Tick(int count)
        {
            for (int i = 0; i < count; i++)
            {
                elapsed += 1.0 / 60;
                world.SetTime(new TimeData(elapsed, 1f / 60));
                system.Update();
                Manager.CompleteAllTrackedJobs();
            }
        }

        public void TickMovement()
        {
            world.GetOrCreateSystem<UnitMoveSystem>().Update(world.Unmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        public void Dispose() { world.Dispose(); Registry.Dispose(); }
    }
}
