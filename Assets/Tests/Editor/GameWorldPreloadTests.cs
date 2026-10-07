using System;
using System.Collections;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Unit;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Hash128 = Unity.Entities.Hash128;

public sealed class GameWorldPreloadTests
{
    private GameObject dataRoot;
    private World originalDefault;
    private Hash128 registryGuid;

    [SetUp]
    public void SetUp()
    {
        originalDefault = World.DefaultGameObjectInjectionWorld;
        GameWorldManager.Shutdown();
        dataRoot = new GameObject("World preload dependencies");
        dataRoot.AddComponent<ResourceComponent>().Initialize();
        dataRoot.AddComponent<ConfigComponent>().Initialize();
        dataRoot.AddComponent<DataComponent>().Initialize();
        registryGuid = GuidFor(DungeonState.RegistrySubSceneName);
    }

    [TearDown]
    public void TearDown()
    {
        GameWorldManager.Shutdown();
        dataRoot.GetComponent<DataComponent>().Cleanup();
        dataRoot.GetComponent<ConfigComponent>().Cleanup();
        dataRoot.GetComponent<ResourceComponent>().Cleanup();
        UnityEngine.Object.DestroyImmediate(dataRoot);
        World.DefaultGameObjectInjectionWorld = originalDefault;
    }

    [TestCase(GameWorldRole.Standalone)]
    [TestCase(GameWorldRole.Client)]
    [TestCase(GameWorldRole.Server)]
    public void ConfiguredWorldRunsOnlyTheSimulationAndPresentationForItsRole(GameWorldRole role)
    {
        using World world = GameWorldManager.CreateConfiguredWorld(role, "World role isolation " + role);
        bool authority = role != GameWorldRole.Client;
        bool visible = role != GameWorldRole.Server;
        Assert.That(world.GetExistingSystemManaged<PlayerInputBridgeSystem>() != null, Is.EqualTo(visible),
            "The server must receive network inputs, never read the host keyboard/mouse again.");
        Assert.That(world.GetExistingSystemManaged<UnitDropOnDestroySystem>() != null, Is.EqualTo(authority),
            "Only authority may roll loot when an entity is destroyed.");
        Assert.That(world.GetExistingSystem<SkillProjectileSystem>() != SystemHandle.Null, Is.EqualTo(authority),
            "Network projectiles on clients must not run authoritative hit/destroy effects.");
        Assert.That(world.GetExistingSystem<DropScatterSystem>() != SystemHandle.Null, Is.EqualTo(authority));
        Assert.That(world.GetExistingSystem<DungeonTreasureRewardSystem>() != SystemHandle.Null, Is.EqualTo(authority),
            "打开宝箱后的奖励必须由单机或服务器生成。");
        Assert.That(world.GetExistingSystemManaged<BehaviorTreeSystem>() != null, Is.EqualTo(authority));
        Assert.That(world.GetExistingSystem<UnitBuffSystem>() != SystemHandle.Null, Is.EqualTo(authority),
            "Clients must not execute authoritative buff triggers.");
        Assert.That(world.GetExistingSystem<UnitModifierSystem>() != SystemHandle.Null, Is.True,
            "Clients still derive skill/chant modifiers from replicated buff snapshots.");
        Assert.That(GameSingletonUtility.Get<BuffEffectRegistryComponent>(world.EntityManager).Value.IsCreated, Is.True);
        Assert.That(world.GetExistingSystemManaged<UnitAnimationSystem>() != null, Is.EqualTo(visible));
        Assert.That(world.GetExistingSystemManaged<ClientSkillVisualExecutionSystem>() != null,
            Is.EqualTo(role == GameWorldRole.Client));
        Assert.That(world.GetExistingSystem<ClientDropScatterPresentationSystem>() != SystemHandle.Null,
            Is.EqualTo(role == GameWorldRole.Client));
        Assert.That(world.GetExistingSystemManaged<FrameReceiveSystem>() != null,
            Is.EqualTo(role != GameWorldRole.Standalone));
        Assert.That(world.GetExistingSystemManaged<ServerNetworkStateCollectSystem>() != null,
            Is.EqualTo(role == GameWorldRole.Server));
        Assert.That(world.GetExistingSystem<SelfUnitAnimationSystem>() != SystemHandle.Null,
            Is.EqualTo(visible), "客户端必须根据宝箱和出口状态更新动画。");
        Assert.That(world.GetExistingSystem<Unity.Scenes.SceneSystem>() != SystemHandle.Null, Is.True,
            "Role filtering must retain common scene streaming.");
        Assert.That(world.GetExistingSystemManaged<StateScriptSystem>(), Is.Not.Null);
        Assert.That(world.GetExistingSystemManaged<TransformSystemGroup>(), Is.Not.Null);
        var skillDefinitions = world.GetExistingSystemManaged<PlayerSkillDefinitionRegistryInitializationSystem>();
        Assert.That(skillDefinitions, Is.Not.Null, "Client prediction needs the same skill costs and chanting durations.");
        skillDefinitions.Update();
        Assert.That(GameSingletonUtility.Get<PlayerSkillDefinitionRegistryComponent>(world.EntityManager).Value.IsCreated,
            Is.True);
    }

    [Test]
    public void ClientRebuildsChantModifiersFromNetworkBuffsWithoutRunningBuffTriggers()
    {
        const int buffId = 100000;
        DataComponent.Instance.GetTable<BuffData>().Add(new BuffData
        {
            Id = buffId,
            PropertyModifiers = new()
            {
                new PropertyModifierEntry { Channel = PropertyModifierChannel.ChantSpeed, Factor = 0.5f },
            },
        });
        using World world = GameWorldManager.CreateConfiguredWorld(GameWorldRole.Client, "Client buff snapshot");
        EntityManager manager = world.EntityManager;
        Guid id = Guid.NewGuid();
        Entity player = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(UnitModifierComponent));
        manager.SetComponentData(player, new NetworkIdentityComponent { id = id });
        manager.SetComponentData(player, UnitModifierComponent.CreateIdentity());
        new NetworkBuffStateData
        {
            unitId = id,
            buffs = new() { new NetworkBuffEntryStateData { buffId = buffId, stackCount = 1, endFrame = 100 } },
        }.Apply(new NetworkStateApplyContext(manager, 10, 33));
        float remaining = manager.GetBuffer<UnitBuffElement>(player)[0].RemainingTime;
        world.GetExistingSystem<UnitModifierSystem>().Update(world.Unmanaged);
        manager.CompleteAllTrackedJobs();
        Assert.That(manager.GetComponentData<UnitModifierComponent>(player).ChantSpeed.Factor, Is.EqualTo(1.5f));
        Assert.That(manager.GetComponentData<UnitBuffComponent>(player).ModifierDirty, Is.Zero);
        Assert.That(manager.GetBuffer<UnitBuffElement>(player)[0].RemainingTime, Is.EqualTo(remaining),
            "Rebuilding prediction modifiers must not tick or trigger the authoritative buff.");
        Assert.That(world.GetExistingSystem<UnitBuffSystem>(), Is.EqualTo(SystemHandle.Null));
    }

    [UnityTest]
    public IEnumerator ServerTreasureRewardsAndClientChestAnimationsFollowReplicatedState()
    {
        GameWorldPreload.Request(GameWorldRole.Server, registryGuid);
        GameWorldPreload.Request(GameWorldRole.Client, registryGuid);
        yield return AwaitReady(GameWorldRole.Server, GameWorldRole.Client);
        World server = GameWorldPreload.GetWorld(GameWorldRole.Server);
        World client = GameWorldPreload.GetWorld(GameWorldRole.Client);
        EntityManager serverManager = server.EntityManager;
        EntityManager clientManager = client.EntityManager;
        ConfigComponent.Instance.Get<DungeonConfig>().ChestRewardCountRange = new Vector2Int(1, 1);
        int itemId = DataComponent.Instance.Find<ItemData>(item => item != null).Id;
        var frame = new ServerFrameManager { running = true, currentFrame = 20 };
        serverManager.AddComponentObject(serverManager.CreateEntity(), new FrameManagerComponent { manager = frame });
        var collect = server.GetExistingSystemManaged<ServerNetworkStateCollectSystem>();
        using EntityQuery scopes = serverManager.CreateEntityQuery(typeof(BattleSimulationScope));
        Entity scope = scopes.IsEmptyIgnoreFilter
            ? serverManager.CreateEntity(typeof(BattleSimulationScope))
            : scopes.GetSingletonEntity();
        using EntityQuery drops = serverManager.CreateEntityQuery(typeof(DropScatterComponent));
        int initialDrops = drops.CalculateEntityCount();

        foreach (DungeonTreasureQuality quality in new[]
                 { DungeonTreasureQuality.Copper, DungeonTreasureQuality.Silver, DungeonTreasureQuality.Gold })
        {
            var info = NetworkEntitySpawnUtility.CreateInfo(NetworkEntityPrefabType.Environment, "Treasure", Vector3.zero);
            info.hasTreasureData = true;
            info.treasureRandomSeed = 1;
            info.treasureQuality = quality;
            info.treasureCandidateItemIds = new[] { itemId };
            Assert.That(NetworkEntitySpawnUtility.TrySpawn(serverManager, info, out Entity serverChest), Is.True);
            NetworkEntitySpawnInfo snapshot = NetworkEntitySpawnUtility.CreateSnapshotInfo(serverManager, serverChest);
            Assert.That(snapshot.treasureQuality, Is.EqualTo(quality), "晚加入客户端也必须收到宝箱品质。");
            Assert.That(NetworkEntitySpawnUtility.TrySpawn(clientManager, snapshot, out Entity clientChest), Is.True);
            client.GetExistingSystem<SelfUnitAnimationSystem>().Update(client.Unmanaged);
            clientManager.CompleteAllTrackedJobs();
            Assert.That(clientManager.GetComponentData<UnitAnimationComponent>(clientChest).AnimationName.ToString(),
                Is.EqualTo(quality + "Closed"));

            TreasureComponent treasure = serverManager.GetComponentData<TreasureComponent>(serverChest);
            treasure.IsOpened = 1;
            treasure.NetworkDirty = 1;
            serverManager.SetComponentData(serverChest, treasure);
            serverManager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.Players });
            server.GetExistingSystem<DungeonTreasureRewardSystem>().Update(server.Unmanaged);
            Assert.That(drops.CalculateEntityCount(), Is.EqualTo(initialDrops), "玩家预测阶段不应处理世界宝箱奖励。");
            serverManager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.World });
            server.GetExistingSystem<DungeonTreasureRewardSystem>().Update(server.Unmanaged);
            int afterOpening = drops.CalculateEntityCount();
            Assert.That(afterOpening, Is.EqualTo(++initialDrops), "服务器打开宝箱必须生成奖励。");
            server.GetExistingSystem<DungeonTreasureRewardSystem>().Update(server.Unmanaged);
            Assert.That(drops.CalculateEntityCount(), Is.EqualTo(afterOpening), "宝箱不能重复生成奖励。");
            Assert.That(serverManager.GetComponentData<TreasureComponent>(serverChest).RewardsSpawned, Is.EqualTo(1));

            collect.Update();
            uint stateFrame = frame.currentFrame - 1;
            var state = frame.sendOrder[stateFrame].OfType<NetworkTreasureStateData>()
                .Single(value => value.unitId == info.unitId);
            Assert.That(state.quality, Is.EqualTo(quality));
            state.Apply(new NetworkStateApplyContext(clientManager, stateFrame, 33));
            client.GetExistingSystem<SelfUnitAnimationSystem>().Update(client.Unmanaged);
            clientManager.CompleteAllTrackedJobs();
            Assert.That(clientManager.GetComponentData<UnitAnimationComponent>(clientChest).AnimationName.ToString(),
                Is.EqualTo(quality + "Open"), "客户端必须显示同步后的开箱动画。");
            Assert.That(clientManager.GetComponentData<TreasureComponent>(clientChest).Quality, Is.EqualTo(quality));
            Assert.That(client.GetExistingSystem<DungeonTreasureRewardSystem>(), Is.EqualTo(SystemHandle.Null));
            frame.currentFrame++;
        }
    }

    [UnityTest]
    public IEnumerator AllRolesLoadCommonContentWithoutRunningGameplayOrReplacingTheDefaultWorld()
    {
        GameWorldPreload.Request(GameWorldRole.Standalone, registryGuid);
        GameWorldPreload.Request(GameWorldRole.Client, registryGuid);
        GameWorldPreload.Request(GameWorldRole.Server, registryGuid);
        World defaultBefore = World.DefaultGameObjectInjectionWorld;
        GameWorldPreload.Tick();
        World standalone = GameWorldPreload.GetWorld(GameWorldRole.Standalone);
        Assert.That(standalone, Is.Not.Null);
        Assert.That(GameWorldPreload.GetWorld(GameWorldRole.Client), Is.Null, "One role starts per frame.");
        var simulation = standalone.GetExistingSystemManaged<SimulationSystemGroup>();
        var probe = standalone.GetOrCreateSystemManaged<PreloadGameplayProbeSystem>();
        simulation.AddSystemToUpdateList(probe);
        GameWorldPreload.Request(GameWorldRole.Standalone, registryGuid);
        yield return AwaitReady(GameWorldRole.Standalone, GameWorldRole.Client, GameWorldRole.Server);
        Assert.That(World.DefaultGameObjectInjectionWorld, Is.SameAs(defaultBefore));
        Assert.That(GameWorldManager.HasGameWorld, Is.False);
        Assert.That(probe.Ticks, Is.Zero);
        Assert.That(simulation.Enabled, Is.False);

        foreach (GameWorldRole role in new[] { GameWorldRole.Standalone, GameWorldRole.Client, GameWorldRole.Server })
        {
            World world = GameWorldPreload.GetWorld(role);
            Assert.That(GameWorldContextUtility.Get(world.EntityManager).Role, Is.EqualTo(role));
            Assert.That(GameWorldContextUtility.GetSceneMode(world.EntityManager), Is.EqualTo(GameSceneMode.None));
            if (role != GameWorldRole.Standalone)
            {
                Assert.That(world.GetExistingSystemManaged<BattleSimulationSystemGroup>().Enabled, Is.False);
                Assert.That(world.GetExistingSystemManaged<BattlePlayerSimulationSystemGroup>(), Is.Not.Null);
                Assert.That(FrameManagerUtility.TryGet(world.EntityManager, out FrameManager _), Is.False);
            }
            using EntityQuery liveUnits = world.EntityManager.CreateEntityQuery(typeof(UnitStateScriptComponent));
            Assert.That(liveUnits.CalculateEntityCount(), Is.Zero, role + " must contain templates, not gameplay units.");
            Assert.That(GameSingletonUtility.Get<StateScriptRuntimeRegistryComponent>(world.EntityManager).Value.IsCreated, Is.True);
            Assert.That(GameSingletonUtility.Get<PlayerSkillDefinitionRegistryComponent>(world.EntityManager).Value.IsCreated, Is.True);
            Assert.That(GameSingletonUtility.Get<BuffEffectRegistryComponent>(world.EntityManager).Value.IsCreated, Is.True);
            if (role != GameWorldRole.Client)
            {
                Assert.That(GameSingletonUtility.Get<BehaviorTreeRuntimeRegistryComponent>(world.EntityManager).Value.IsCreated, Is.True);
            }
            using EntityQuery prefabs = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Prefab>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities,
            });
            Assert.That(prefabs.CalculateEntityCount(), Is.GreaterThan(0), role.ToString());
            FixedString128Bytes playerName = "PlayerDungeon";
            Assert.That(EntitySpawnRegistryUtility.TryInstantiateUnit(world.EntityManager, in playerName, out Entity instance),
                Is.True, role + " must be able to spawn from the prepared registry.");
            Assert.That(world.EntityManager.HasComponent<Prefab>(instance), Is.False);
            world.EntityManager.DestroyEntity(instance);
        }
        Assert.That(GameWorldPreload.GetWorld(GameWorldRole.Client), Is.Not.SameAs(standalone));
        Assert.That(GameWorldPreload.GetWorld(GameWorldRole.Server), Is.Not.SameAs(standalone));

        // Reproduce Unity SubScene.OnEnable adding a town/training scene to all Worlds.
        Hash128 foreignGuid = GuidFor(TrainingState.SubSceneName);
        SceneSystem.LoadSceneAsync(standalone.Unmanaged, foreignGuid,
            new SceneSystem.LoadParameters { Flags = SceneLoadFlags.DisableAutoLoad });
        GameWorldPreload.DiscardForeignScenes();
        Assert.That(SceneSystem.GetSceneEntity(standalone.Unmanaged, foreignGuid), Is.EqualTo(Entity.Null));
        Assert.That(SceneSystem.IsSceneLoaded(standalone.Unmanaged,
            SceneSystem.GetSceneEntity(standalone.Unmanaged, registryGuid)), Is.True);
    }

    [UnityTest]
    public IEnumerator SwitchingRolesReusesSystemsAndPrefabsButClearsSceneEntitiesAndFrameBindings()
    {
        GameWorldPreload.Request(GameWorldRole.Standalone, registryGuid);
        GameWorldPreload.Request(GameWorldRole.Client, registryGuid);
        yield return AwaitReady(GameWorldRole.Standalone, GameWorldRole.Client);
        World prepared = GameWorldPreload.GetWorld(GameWorldRole.Standalone);
        var scriptSystem = prepared.GetExistingSystemManaged<StateScriptInitSystem>();
        var scriptRegistry = GameSingletonUtility.Get<StateScriptRuntimeRegistryComponent>(prepared.EntityManager).Value;
        Entity commonScene = SceneSystem.GetSceneEntity(prepared.Unmanaged, registryGuid);
        World active = GameWorldManager.CreateGameWorld(GameWorldRole.Standalone, false);
        Assert.That(active, Is.SameAs(prepared));
        Assert.That(World.DefaultGameObjectInjectionWorld, Is.SameAs(active));
        Assert.That(active.GetExistingSystemManaged<SimulationSystemGroup>().Enabled, Is.True);
        Entity instance = active.EntityManager.CreateEntity(typeof(DungeonRuntimeOwnedEntity));
        Entity sleeper = active.EntityManager.CreateEntity(typeof(DungeonRuntimeOwnedEntity), typeof(Disabled));
        Entity projectile = active.EntityManager.CreateEntity(typeof(SkillProjectileComponent), typeof(DestroyEntityFlag));
        active.EntityManager.SetComponentEnabled<DestroyEntityFlag>(projectile, false);
        Entity template = active.EntityManager.CreateEntity(typeof(Prefab), typeof(DestroyEntityFlag));
        active.EntityManager.SetComponentEnabled<DestroyEntityFlag>(template, false);
        // Loading a slot must happen AFTER the prepared World becomes active.
        StashData stashData = new();
        GameRuntimeStateUtility.ImportPersistentData(new SaveData { Stash = stashData });
        Entity stash;
        using (EntityQuery stashQuery = active.EntityManager.CreateEntityQuery(typeof(StashComponent)))
            stash = stashQuery.GetSingletonEntity();
        Assert.That(active.EntityManager.GetComponentObject<StashComponent>(stash).Data, Is.SameAs(stashData));
        Entity run = active.EntityManager.CreateEntity();
        active.EntityManager.AddComponentObject(run, new DungeonRunComponent());
        GameWorldManager.ReleaseGameWorld();
        Assert.That(active.IsCreated, Is.True);
        Assert.That(active.EntityManager.Exists(instance), Is.False);
        Assert.That(active.EntityManager.Exists(sleeper), Is.False);
        Assert.That(active.EntityManager.Exists(projectile), Is.False, "Living projectiles must not survive parking.");
        Assert.That(active.EntityManager.Exists(template), Is.True, "Common templates must stay loaded.");
        Assert.That(active.EntityManager.Exists(stash), Is.False);
        Assert.That(active.EntityManager.Exists(run), Is.False);
        Assert.That(SceneSystem.IsSceneLoaded(active.Unmanaged, commonScene), Is.True);
        Assert.That(active.GetExistingSystemManaged<StateScriptInitSystem>(), Is.SameAs(scriptSystem));
        Assert.That(GameSingletonUtility.Get<StateScriptRuntimeRegistryComponent>(active.EntityManager).Value, Is.EqualTo(scriptRegistry));

        World client = GameWorldManager.CreateGameWorld(GameWorldRole.Client, false);
        ClientFrameManager firstFrame = new();
        FrameManagerUtility.Bind(client.EntityManager, firstFrame);
        GameWorldManager.ReleaseGameWorld();
        Assert.That(FrameManagerUtility.TryGet(client.EntityManager, out FrameManager _), Is.False);
        Assert.That(client.GetExistingSystemManaged<BattleSimulationSystemGroup>().Enabled, Is.False);
        World clientAgain = GameWorldManager.CreateGameWorld(GameWorldRole.Client, false);
        Assert.That(clientAgain, Is.SameAs(client));
        ClientFrameManager secondFrame = new();
        FrameManagerUtility.Bind(client.EntityManager, secondFrame);
        Assert.That(FrameManagerUtility.TryGet(client.EntityManager, out FrameManager bound), Is.True);
        Assert.That(bound, Is.SameAs(secondFrame));
        Assert.That(client.GetExistingSystemManaged<BattleSimulationSystemGroup>().Enabled, Is.True);
        GameWorldManager.ReleaseGameWorld();
        Assert.That(GameWorldManager.CreateGameWorld(GameWorldRole.Standalone, false), Is.SameAs(prepared));
        GameWorldManager.ReleaseGameWorld();
        GameWorldManager.Shutdown();
        Assert.That(prepared.IsCreated, Is.False);
        Assert.That(client.IsCreated, Is.False);
    }

    [UnityTest]
    public IEnumerator ConsecutiveHostedSessionsTakeTheSamePreparedServerAndKeepItsPresentationDisabled()
    {
        GameWorldPreload.Request(GameWorldRole.Server, registryGuid);
        yield return AwaitReady(GameWorldRole.Server);
        World prepared = GameWorldPreload.GetWorld(GameWorldRole.Server);
        Entity scene = SceneSystem.GetSceneEntity(prepared.Unmanaged, registryGuid);
        using (var first = new BattleWorldContext(11, new ServerFrameManager(), registryGuid))
        {
            Assert.That(first.World, Is.SameAs(prepared));
            Assert.That(first.RegistrySceneEntity, Is.EqualTo(scene));
            Assert.That(first.World.GetExistingSystemManaged<PresentationSystemGroup>().Enabled, Is.False);
            Entity mapEntity = first.EntityManager.CreateEntity(typeof(DungeonRuntimeOwnedEntity));
            first.Dispose();
            Assert.That(prepared.EntityManager.Exists(mapEntity), Is.False);
        }
        Assert.That(prepared.IsCreated, Is.True);
        using (var second = new BattleWorldContext(12, new ServerFrameManager(), registryGuid))
        {
            Assert.That(second.World, Is.SameAs(prepared));
            Assert.That(second.World.GetExistingSystemManaged<PresentationSystemGroup>().Enabled, Is.False);
            // A town enable while the old host is finishing must not add server units.
            Hash128 townGuid = GuidFor(TownState.SubSceneName);
            SceneSystem.LoadSceneAsync(prepared.Unmanaged, townGuid,
                new SceneSystem.LoadParameters { Flags = SceneLoadFlags.DisableAutoLoad });
            GameWorldPreload.DiscardForeignScenes();
            Assert.That(SceneSystem.GetSceneEntity(prepared.Unmanaged, townGuid), Is.EqualTo(Entity.Null));
        }
    }

    [UnityTest]
    public IEnumerator TownAndTrainingLoadTheirActorsAndKeepOneCommonPrefabRegistry()
    {
        GameWorldPreload.Request(GameWorldRole.Standalone, registryGuid);
        yield return AwaitReady(GameWorldRole.Standalone);
        World world = GameWorldManager.CreateGameWorld(GameWorldRole.Standalone, false);
        FixedString128Bytes playerName = "PlayerDungeon";
        Assert.That(EntitySpawnRegistryUtility.TryGetUnitPrefab(world.EntityManager, in playerName, out Entity commonPrefab), Is.True);
        foreach (string sceneName in new[] { TownState.SubSceneName, TrainingState.SubSceneName })
        {
            Entity scene = SceneSystem.LoadSceneAsync(world.Unmanaged, GuidFor(sceneName));
            double deadline = EditorApplication.timeSinceStartup + GameWorldPreload.TimeoutSeconds;
            while (!SceneSystem.IsSceneLoaded(world.Unmanaged, scene) && EditorApplication.timeSinceStartup < deadline)
            {
                world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
                yield return null;
            }
            Assert.That(SceneSystem.IsSceneLoaded(world.Unmanaged, scene), Is.True, sceneName);
            using (EntityQuery registries = world.EntityManager.CreateEntityQuery(typeof(EntitySpawnRegistrySingleton)))
                Assert.That(registries.CalculateEntityCount(), Is.EqualTo(1), sceneName + " must reuse the common registry.");
            using (EntityQuery actors = world.EntityManager.CreateEntityQuery(typeof(UnitFactionComponent)))
                Assert.That(actors.CalculateEntityCount(), Is.GreaterThan(0), sceneName + " must still load its own actors.");
            Assert.That(EntitySpawnRegistryUtility.TryGetUnitPrefab(world.EntityManager, in playerName, out Entity prefab), Is.True);
            Assert.That(prefab, Is.EqualTo(commonPrefab));
            SceneSystem.UnloadScene(world.Unmanaged, scene, SceneSystem.UnloadParameters.DestroyMetaEntities);
            Assert.That(world.EntityManager.Exists(commonPrefab), Is.True);
        }
        GameWorldManager.ReleaseGameWorld();
    }

    private static IEnumerator AwaitReady(params GameWorldRole[] roles)
    {
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + GameWorldPreload.TimeoutSeconds + 5;
        while (UnityEditor.EditorApplication.timeSinceStartup < deadline)
        {
            GameWorldPreload.Tick();
            bool ready = true;
            foreach (GameWorldRole role in roles)
            {
                Assert.That(GameWorldPreload.GetError(role), Is.Null, role.ToString());
                ready &= GameWorldPreload.IsReady(role);
            }
            if (ready) yield break;
            yield return null;
        }
        Assert.Fail("Common World preloading did not finish.");
    }

    private static Hash128 GuidFor(string name) =>
        new(AssetDatabase.AssetPathToGUID($"Assets/Scenes/SubScene/{name}.unity"));
}

[DisableAutoCreation]
public partial class PreloadGameplayProbeSystem : SystemBase
{
    public int Ticks;
    protected override void OnUpdate() => Ticks++;
}
