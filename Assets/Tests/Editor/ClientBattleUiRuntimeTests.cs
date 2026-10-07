using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed class ClientBattleUiRuntimeTests
{
    private static readonly MethodInfo ReadChant = typeof(BattleUIModel).GetMethod(
        "TryGetChantProgress", BindingFlags.Static | BindingFlags.NonPublic);

    [TestCase(GameWorldRole.Standalone)]
    [TestCase(GameWorldRole.Client)]
    [TestCase(GameWorldRole.Server)]
    public void ActualPlayerGraphStartsAndAdvancesChantForEveryRole(GameWorldRole role)
    {
        using RuntimeWorld fixture = new(role);
        fixture.Tick();
        fixture.Tick();
        PlayerInputEventUtility.Append(fixture.Manager, fixture.Player, PlayerInputOperationType.PrimaryPressed,
            new PlayerInputComponent { IsPrimaryHeld = 1 });
        fixture.Manager.SetComponentData(fixture.Player, new PlayerInputComponent { IsPrimaryHeld = 1 });
        fixture.Tick();
        fixture.Manager.GetBuffer<PlayerInputEventElement>(fixture.Player).Clear();
        for (int index = 0; index < 5; index++)
            fixture.Tick();

        Assert.That(PlayerCurrentSkillUtility.IsCasting(fixture.Manager, fixture.Player), Is.True);
        Assert.That(fixture.Manager.GetComponentData<PlayerCurrentSkillComponent>(fixture.Player).CurrentSlotIndex,
            Is.EqualTo(0));
        object[] arguments = { fixture.Manager, fixture.Player, 0f };
        Assert.That(ReadChant.Invoke(null, arguments), Is.True);
        Assert.That((float)arguments[2], Is.GreaterThan(0f).And.LessThan(1f));
    }

    [Test]
    public void ActualClientQueryFindsNearbyDropsAndRemovesFarOrDestroyedCandidates()
    {
        using RuntimeWorld fixture = new(GameWorldRole.Client);
        Entity drop = fixture.Manager.CreateEntity(typeof(LocalTransform), typeof(UnitFactionComponent),
            typeof(UnitStateScriptComponent), typeof(UnitInteractableComponent), typeof(DestroyEntityFlag));
        fixture.Manager.SetComponentEnabled<DestroyEntityFlag>(drop, false);
        fixture.Manager.SetComponentData(drop, LocalTransform.FromPosition(new float3(1, 0, 0)));
        fixture.Manager.SetComponentData(drop, new UnitFactionComponent { Value = UnitFactionType.Interactable });
        fixture.Manager.SetComponentData(drop, new UnitStateScriptComponent { UnitDataId = 3, DefinitionIndex = -1 });
        fixture.Manager.SetComponentData(drop, new UnitInteractableComponent
        {
            Data = UnitInteractionData.CreateDrop(DropRewardType.Item, 1, 1), IsEnabled = 1, RangeSq = 16,
        });
        fixture.Tick();
        fixture.Tick();
        Assert.That(fixture.Candidate(), Is.EqualTo(drop));

        fixture.Manager.SetComponentData(drop, LocalTransform.FromPosition(new float3(10, 0, 0)));
        fixture.Tick();
        Assert.That(fixture.Candidate(), Is.EqualTo(Entity.Null));
        fixture.Manager.SetComponentData(drop, LocalTransform.FromPosition(new float3(1, 0, 0)));
        fixture.Manager.SetComponentEnabled<DestroyEntityFlag>(drop, true);
        fixture.Tick();
        Assert.That(fixture.Candidate(), Is.EqualTo(Entity.Null));
        Entity interactions = GameSingletonUtility.GetEntity<GameInteractionComponent>(fixture.Manager);
        Assert.That(fixture.Manager.GetBuffer<InteractionTransactionElement>(interactions).Length, Is.Zero,
            "查询提示不能在客户端创建拾取事务或发放物品。");
    }

    [Test]
    public void ClientExitInteractionRunsTheLocalTargetsGraphDuringTheWorldPass()
    {
        using RuntimeWorld fixture = new(GameWorldRole.Client, 32);
        Entity exit = fixture.CreateInteractable(32, InteractionKind.Npc, new float3(1, 0, 0));
        Entity otherExit = fixture.CreateInteractable(32, InteractionKind.Npc, new float3(2, 0, 0));
        fixture.EnableManagedCommands();
        fixture.Tick();
        fixture.Tick();
        Entity runtime = GameSingletonUtility.GetEntity<GameInteractionComponent>(fixture.Manager);
        Entity otherActor = fixture.Manager.CreateEntity(typeof(LocalTransform));
        fixture.Manager.SetComponentData(otherActor, LocalTransform.Identity);
        Assert.That(GameInteractionUtility.TryRequest(fixture.Manager, runtime, otherActor, otherExit), Is.True);
        PlayerInputEventUtility.Append(fixture.Manager, fixture.Player, PlayerInputOperationType.Interact, default);
        fixture.Tick();
        fixture.Manager.GetBuffer<PlayerInputEventElement>(fixture.Player).Clear();
        using (var transactions = fixture.Manager.GetBuffer<InteractionTransactionElement>(runtime)
                   .ToNativeArray(Allocator.Temp))
            Assert.That(transactions.Any(transaction => transaction.Actor == fixture.Player && transaction.Target == exit),
                Is.True, "本地出口请求必须选中出口。");

        fixture.SetPass(BattleSimulationPass.World);
        bool started = false;
        for (int tick = 0; tick < 3; tick++)
        {
            fixture.Tick(processManaged: false);
            var commands = fixture.Manager.GetBuffer<StateScriptManagedCommandElement>(
                StateScriptManagedCommandQueueUtility.GetOrCreateEntity(fixture.Manager));
            for (int index = 0; index < commands.Length; index++)
            {
                Assert.That(commands[index].SourceEntity, Is.Not.EqualTo(otherExit), "不能启动其他玩家的交互界面。");
                started |= commands[index].SourceEntity == exit &&
                           commands[index].Type == StateScriptManagedCommandType.StartNpcInteraction;
            }
        }
        Assert.That(started, Is.True, "客户端世界阶段必须执行出口脚本，才能启动选择界面。");
        Assert.That(fixture.Manager.GetComponentData<UnitStateScriptComponent>(otherExit).TickVersion, Is.Zero,
            "客户端不能因为修复出口交互而运行其他目标的状态脚本。");

        Assert.That(GameInteractionUtility.TryBegin(fixture.Manager, runtime, exit, out _), Is.True);
        Assert.That(GameInteractionUtility.Complete(fixture.Manager, runtime, exit,
            InteractionResultCode.Cancelled, UnitSourceValue.FromBool(false)), Is.True);
        fixture.SetPass(BattleSimulationPass.Players);
        fixture.Tick(); // 玩家图确认关闭交互结果。
        Assert.That(fixture.Manager.GetBuffer<InteractionTransactionElement>(runtime).Length, Is.EqualTo(1),
            "本人的已完成事务应被清除，其他玩家的事务应保留。");
        fixture.SetPass(BattleSimulationPass.World);
        fixture.Tick(processManaged: false); // 目标必须还能求值一次，清理已结束的交互。
        uint afterCleanup = fixture.Manager.GetComponentData<UnitStateScriptComponent>(exit).TickVersion;
        fixture.Tick(processManaged: false);
        Assert.That(fixture.Manager.GetComponentData<UnitStateScriptComponent>(exit).TickVersion, Is.EqualTo(afterCleanup),
            "交互结束后不能一直执行目标脚本。");
        Assert.That(GameInteractionUtility.TryRequest(fixture.Manager, runtime, fixture.Player, exit), Is.True);
        fixture.Tick(processManaged: false);
        var repeatedCommands = fixture.Manager.GetBuffer<StateScriptManagedCommandElement>(
            StateScriptManagedCommandQueueUtility.GetOrCreateEntity(fixture.Manager));
        Assert.That(repeatedCommands.Length, Is.GreaterThan(0), "取消选择后应能重新启动出口交互。");
        Assert.That(repeatedCommands[0].Type, Is.EqualTo(StateScriptManagedCommandType.StartNpcInteraction));
    }

    [Test]
    public void ClientTreasurePromptDoesNotCreateALocalExitTransaction()
    {
        using RuntimeWorld fixture = new(GameWorldRole.Client, 33);
        Entity treasure = fixture.CreateInteractable(33, InteractionKind.Treasure, new float3(1, 0, 0));
        fixture.EnableManagedCommands();
        fixture.Tick();
        fixture.Tick();
        Assert.That(fixture.Candidate(), Is.EqualTo(treasure));
        PlayerInputEventUtility.Append(fixture.Manager, fixture.Player, PlayerInputOperationType.Interact, default);
        fixture.Tick();
        Entity runtime = GameSingletonUtility.GetEntity<GameInteractionComponent>(fixture.Manager);
        Assert.That(fixture.Manager.GetBuffer<InteractionTransactionElement>(runtime).Length, Is.Zero,
            "宝箱应由网络请求交给服务器，不能被客户端出口图创建成本地 NPC 事务。");
        Assert.That(fixture.Manager.GetComponentData<TreasureComponent>(treasure).IsOpened, Is.Zero);
    }

    [Test]
    public void NetworkInteractOpensTreasureInTheServerWorldPass()
    {
        using RuntimeWorld fixture = new(GameWorldRole.Server, 33);
        Entity treasure = fixture.CreateInteractable(33, InteractionKind.Treasure, new float3(1, 0, 0));
        fixture.EnableManagedCommands();
        fixture.Tick();
        fixture.Tick();
        Guid playerId = Guid.NewGuid();
        fixture.Manager.AddComponentData(fixture.Player, new NetworkIdentityComponent { id = playerId });
        new NetworkInteractData { unitId = playerId }.Apply(new NetworkStateApplyContext(fixture.Manager, 10, 33));
        fixture.Tick();
        fixture.Manager.GetBuffer<PlayerInputEventElement>(fixture.Player).Clear();
        fixture.SetPass(BattleSimulationPass.World);
        for (int tick = 0; tick < 3; tick++)
            fixture.Tick();
        Assert.That(fixture.Manager.GetComponentData<TreasureComponent>(treasure).IsOpened, Is.EqualTo(1));
        Assert.That(fixture.Manager.GetComponentData<UnitInteractableComponent>(treasure).IsEnabled, Is.Zero);
        Entity runtime = GameSingletonUtility.GetEntity<GameInteractionComponent>(fixture.Manager);
        Assert.That(fixture.Manager.GetBuffer<InteractionTransactionElement>(runtime)[0].Phase,
            Is.EqualTo(InteractionPhase.Succeeded));
    }

    [Test]
    public void ReconciliationRestoresChantVariablesCurrentSkillAndManaTogether()
    {
        using RuntimeWorld fixture = new(GameWorldRole.Client);
        fixture.Tick();
        var originalResults = fixture.Manager.GetBuffer<StateScriptExternalResultElement>(fixture.Player);
        originalResults.Add(new StateScriptExternalResultElement
            { GraphIndex = 1, NodeIndex = 2, ExecutionVersion = 3, Status = StateScriptExternalResultStatus.Completed });
        var snapshot = ClientPlayerPredictionSnapshot.Capture(fixture.Manager, fixture.Player, Guid.NewGuid());
        fixture.Manager.SetComponentData(fixture.Player, new PlayerCurrentSkillComponent
            { CurrentChainId = 0, CurrentSlotIndex = 0 });
        fixture.Manager.SetComponentData(fixture.Player, new UnitManaComponent { CurrentMana = 3 });
        fixture.Manager.GetBuffer<UnitVariableElement>(fixture.Player).Clear();
        fixture.Manager.GetBuffer<UnitVariableElement>(fixture.Player).Add(new UnitVariableElement
            { Key = PlayerCurrentSkillUtility.CastingVariableKey, Value = UnitSourceValue.FromBool(true) });
        fixture.Manager.GetBuffer<StateScriptExternalResultElement>(fixture.Player).Clear();
        Assert.That(snapshot.RestoreStateScript(fixture.Manager, fixture.Player), Is.True);
        Assert.That(PlayerCurrentSkillUtility.IsCasting(fixture.Manager, fixture.Player), Is.False);
        Assert.That(fixture.Manager.GetComponentData<PlayerCurrentSkillComponent>(fixture.Player).CurrentSlotIndex,
            Is.EqualTo(-1));
        Assert.That(fixture.Manager.GetComponentData<UnitManaComponent>(fixture.Player).CurrentMana, Is.EqualTo(100));
        Assert.That(fixture.Manager.GetBuffer<UnitVariableElement>(fixture.Player).Length,
            Is.EqualTo(snapshot.Variables.Length));
        Assert.That(fixture.Manager.GetBuffer<StateScriptExternalResultElement>(fixture.Player)[0].ExecutionVersion,
            Is.EqualTo(3));
    }

    [Test]
    public void ReplayedCastFinishesAdditionNodesAndClosesTheChantBar()
    {
        using RuntimeWorld fixture = new(GameWorldRole.Client);
        fixture.EnableManagedCommands();
        fixture.Tick(replaySkills: true);
        fixture.Tick(replaySkills: true);
        PlayerInputEventUtility.Append(fixture.Manager, fixture.Player, PlayerInputOperationType.PrimaryPressed,
            new PlayerInputComponent { IsPrimaryHeld = 1 });
        fixture.Tick(replaySkills: true);
        fixture.Manager.GetBuffer<PlayerInputEventElement>(fixture.Player).Clear();
        Assert.That(PlayerCurrentSkillUtility.IsCasting(fixture.Manager, fixture.Player), Is.True);
        for (int index = 0; index < 50; index++)
            fixture.Tick(replaySkills: true);
        Assert.That(PlayerCurrentSkillUtility.IsCasting(fixture.Manager, fixture.Player), Is.False);
        Assert.That(fixture.Manager.GetComponentData<UnitManaComponent>(fixture.Player).CurrentMana, Is.EqualTo(90));
        object[] arguments = { fixture.Manager, fixture.Player, 0f };
        Assert.That(ReadChant.Invoke(null, arguments), Is.False);
    }

    [Test]
    public void PickupDespawnIsSentDuringPlayerTickOnceAndClientRemovesTheEntity()
    {
        using World server = new("Pickup despawn authority", WorldFlags.GameServer);
        var manager = server.EntityManager;
        var frame = new ServerFrameManager { running = true, currentFrame = 7 };
        manager.AddComponentObject(manager.CreateEntity(), new FrameManagerComponent { manager = frame });
        Entity scope = manager.CreateEntity(typeof(BattleSimulationScope));
        manager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.Players });
        Entity drop = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(DestroyEntityFlag));
        Guid id = Guid.NewGuid();
        manager.SetComponentData(drop, new NetworkIdentityComponent { id = id });
        var collect = server.GetOrCreateSystemManaged<ServerNetworkStateCollectSystem>();
        collect.Update();
        var despawn = frame.sendOrder[7].OfType<NetworkEntityDespawnStateData>().Single();
        manager.SetComponentData(scope, new BattleSimulationScope { Pass = BattleSimulationPass.World });
        collect.Update();
        Assert.That(frame.sendOrder.Values.SelectMany(states => states).OfType<NetworkEntityDespawnStateData>().Count(),
            Is.EqualTo(1));

        using World client = new("Pickup despawn presentation", WorldFlags.GameClient);
        GameSingletonUtility.Create(client.EntityManager, GameWorldRole.Client, GameSceneMode.Dungeon);
        Entity clientDrop = client.EntityManager.CreateEntity(typeof(NetworkIdentityComponent));
        client.EntityManager.SetComponentData(clientDrop, new NetworkIdentityComponent { id = id });
        GameObject visual = new("Drop pickup sprite", typeof(SpriteRenderer));
        try
        {
            client.EntityManager.AddComponentObject(clientDrop, visual.GetComponent<SpriteRenderer>());
            Type referenceType = typeof(Baker<>).Assembly.GetType("Unity.Entities.CompanionReference", true);
            object reference = Activator.CreateInstance(referenceType);
            referenceType.GetField("Companion").SetValue(reference, (UnityObjectRef<GameObject>)visual);
            client.EntityManager.AddComponentObject(clientDrop, reference);
            despawn.Apply(new NetworkStateApplyContext(client.EntityManager, 7, 33));
            client.GetOrCreateSystemManaged<ClientEntityLifetimePresentationSystem>().Update();
            var presentation = client.GetOrCreateSystemManaged<GamePresentationSystemGroup>();
            presentation.AddSystemToUpdateList(client.GetOrCreateSystem<DestroyEntitySystem>());
            presentation.Update();
            Assert.That(client.EntityManager.HasComponent<NetworkIdentityComponent>(clientDrop), Is.False);
            Assert.That(visual == null, Is.True, "掉落物的 Companion Sprite 必须与实体一起移除。");
        }
        finally
        {
            if (visual != null)
                UnityEngine.Object.DestroyImmediate(visual);
        }
    }

    private sealed class RuntimeWorld : IDisposable
    {
        private readonly World _world;
        private readonly BlobAssetReference<StateScriptRuntimeRegistryBlob> _scripts;
        private readonly BlobAssetReference<PlayerSkillDefinitionRegistryBlob> _skills;
        private readonly StateScriptSystem _system;
        private readonly UnitInitializationSystemGroup _initialization;
        private readonly StateScriptData _playerData;
        private GameObject _dataRoot;
        private StateScriptManagedCommandSystem _managed;
        private double _elapsed;
        public EntityManager Manager => _world.EntityManager;
        public Entity Player { get; }

        public RuntimeWorld(GameWorldRole role, params int[] targetUnitIds)
        {
            _world = new World("Battle UI runtime " + role, role == GameWorldRole.Client
                ? WorldFlags.GameClient : role == GameWorldRole.Server ? WorldFlags.GameServer : WorldFlags.Game);
            GameSingletonUtility.Create(Manager, role, GameSceneMode.Dungeon);
            var rows = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,
                "Res/Data/StateScriptDataTable.json")))["Rows"];
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
                { TypeNameHandling = TypeNameHandling.Auto, NullValueHandling = NullValueHandling.Ignore });
            var player = rows.Single(row => (int)row["Id"] == 2).ToObject<StateScriptData>(serializer);
            _playerData = player;
            StateScriptData[] scripts = new[] { player }.Concat(targetUnitIds.Select(id =>
                rows.Single(row => (int)row["Id"] == id).ToObject<StateScriptData>(serializer))).ToArray();
            Assert.That(StateScriptCompiler.TryBuildRegistry(scripts,
                out _scripts, out string error), Is.True, error);
            Manager.SetComponentData(Manager.CreateEntity(typeof(StateScriptRuntimeRegistryComponent)),
                new StateScriptRuntimeRegistryComponent { Value = _scripts });
            using (BlobBuilder builder = new(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<PlayerSkillDefinitionRegistryBlob>();
                builder.Allocate(ref root.Skills, 1)[0] = new PlayerSkillDefinitionBlob
                {
                    Id = 1, MpCost = 10, ChantDuration = 1, CastingMoveMultiplier = 0.5f,
                    InputType = SkillInputType.MousePosition,
                };
                builder.Allocate(ref root.ModifierMinimumFactors, 0);
                _skills = builder.CreateBlobAssetReference<PlayerSkillDefinitionRegistryBlob>(Allocator.Persistent);
            }
            GameSingletonUtility.Set(Manager, new PlayerSkillDefinitionRegistryComponent { Value = _skills });
            Manager.SetComponentData(Manager.CreateEntity(typeof(BattleSimulationScope)),
                new BattleSimulationScope { Pass = BattleSimulationPass.Players });
            Player = Manager.CreateEntity(typeof(UnitStateScriptComponent), typeof(PlayerInputComponent),
                typeof(NetworkPlayerComponent), typeof(UnitVariableComponent), typeof(PlayerCurrentSkillComponent),
                typeof(UnitManaComponent), typeof(UnitAttackComponent), typeof(UnitMoveComponent),
                typeof(LocalTransform), typeof(UnitFactionComponent), typeof(UnitControlRuntimeComponent));
            Manager.SetComponentData(Player, new UnitFactionComponent { Value = UnitFactionType.Player });
            Manager.SetComponentData(Player, LocalTransform.Identity);
            Manager.SetComponentData(Player, new UnitStateScriptComponent { UnitDataId = 2, DefinitionIndex = 0 });
            Manager.SetComponentData(Player, new PlayerCurrentSkillComponent
                { CurrentChainId = -1, CurrentSlotIndex = -1 });
            Manager.SetComponentData(Player, new UnitManaComponent { BaseMaxMp = 100, CurrentMana = 100 });
            Manager.AddBuffer<UnitVariableElement>(Player);
            Manager.AddBuffer<UnitVariableConsumerElement>(Player);
            Manager.AddBuffer<PlayerInputEventElement>(Player);
            Manager.AddBuffer<PlayerSkillChainElement>(Player).Add(new PlayerSkillChainElement { SlotCount = 1 });
            Manager.AddBuffer<PlayerSkillChainSlotElement>(Player).Add(new PlayerSkillChainSlotElement
                { SkillId = 1, SkillAdditionId = -1 });
            InitializeScriptBuffers(Player, 0);
            _initialization = _world.GetOrCreateSystemManaged<UnitInitializationSystemGroup>();
            _initialization.AddSystemToUpdateList(_world.GetOrCreateSystem<UnitQueryBuildSystem>());
            _system = _world.GetOrCreateSystemManaged<StateScriptSystem>();
        }

        private void InitializeScriptBuffers(Entity entity, int definitionIndex)
        {
            Manager.AddBuffer<StateScriptSourceCommandElement>(entity);
            Manager.AddBuffer<StateScriptSourceCommandArgumentElement>(entity);
            Manager.AddBuffer<StateScriptExternalResultElement>(entity);
            Manager.AddBuffer<StateScriptGraphStateElement>(entity);
            Manager.AddBuffer<StateScriptNodeStateElement>(entity);
            int start = 0;
            for (int graph = 0; graph < _scripts.Value.Units[definitionIndex].Graphs.Length; graph++)
            {
                Manager.GetBuffer<StateScriptGraphStateElement>(entity).Add(
                    new StateScriptGraphStateElement { NodeStateStart = start });
                start += _scripts.Value.Units[definitionIndex].Graphs[graph].Nodes.Length;
            }
            var nodeStates = Manager.GetBuffer<StateScriptNodeStateElement>(entity);
            nodeStates.ResizeUninitialized(start);
            for (int index = 0; index < start; index++)
                nodeStates[index] = default;
        }

        public Entity CreateInteractable(int unitDataId, InteractionKind kind, float3 position)
        {
            int definitionIndex = StateScriptCompiler.FindUnitIndex(_scripts, unitDataId);
            Entity entity = Manager.CreateEntity(typeof(LocalTransform), typeof(UnitFactionComponent),
                typeof(UnitStateScriptComponent), typeof(UnitVariableComponent), typeof(UnitInteractableComponent));
            Manager.SetComponentData(entity, LocalTransform.FromPosition(position));
            Manager.SetComponentData(entity, new UnitFactionComponent { Value = UnitFactionType.Interactable });
            Manager.SetComponentData(entity, new UnitStateScriptComponent
                { UnitDataId = unitDataId, DefinitionIndex = definitionIndex });
            Manager.SetComponentData(entity, new UnitInteractableComponent
                { Data = new UnitInteractionData { Kind = kind }, IsEnabled = 1, RangeSq = 16 });
            Manager.AddBuffer<UnitVariableElement>(entity);
            Manager.AddBuffer<UnitVariableConsumerElement>(entity);
            if (kind == InteractionKind.Treasure)
                Manager.AddComponent<TreasureComponent>(entity);
            InitializeScriptBuffers(entity, definitionIndex);
            return entity;
        }

        public void SetPass(BattleSimulationPass pass) => Manager.SetComponentData(
            GameSingletonUtility.GetEntity<BattleSimulationScope>(Manager), new BattleSimulationScope { Pass = pass });

        public void EnableManagedCommands()
        {
            _dataRoot = new GameObject("Client casting runtime data", typeof(DataComponent));
            var data = _dataRoot.GetComponent<DataComponent>();
            var table = new DataTable<StateScriptData>();
            table.Add(_playerData);
            var tables = (Dictionary<Type, object>)typeof(DataComponent).GetField("_tables",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
            tables[typeof(StateScriptData)] = table;
            _managed = _world.GetOrCreateSystemManaged<StateScriptManagedCommandSystem>();
        }

        public void Tick(bool replaySkills = false, bool processManaged = true)
        {
            _elapsed += 0.033;
            _world.SetTime(new TimeData(_elapsed, 0.033f));
            _initialization.Update();
            _system.Update();
            Manager.CompleteAllTrackedJobs();
            if (replaySkills)
                _managed?.ReplayClientSkills();
            else if (processManaged)
                _managed?.Update();
            var state = Manager.GetComponentData<UnitStateScriptComponent>(Player);
            Assert.That(state.TickVersion, Is.GreaterThan(0), "玩家图没有进入求值。");
            Assert.That(state.InitializationError, Is.EqualTo(StateScriptInitializationError.None),
                "玩家图运行失败，Tick=" + state.TickVersion);
        }

        public Entity Candidate() => UnitVariableSource.TryGetValue(Manager, Player,
            "game.interaction.candidates.0", out UnitSourceValue value) && value.TryGetEntity(out Entity candidate)
            ? candidate : Entity.Null;

        public void Dispose()
        {
            _world.Dispose();
            _scripts.Dispose();
            _skills.Dispose();
            if (_dataRoot != null)
            {
                _dataRoot.GetComponent<DataComponent>().Cleanup();
                UnityEngine.Object.DestroyImmediate(_dataRoot);
            }
        }
    }
}
