using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Server;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using System.Reflection;

public sealed class BattleLifecycleTests
{
    [Test]
    public void RemovedConnectionCannotLeaveCommandsForReloadedPlayer()
    {
        TCPPacketCode.Init();
        ServerFrameManager frame = new() { sceneVersion = 1, currentFrame = 10 };
        Connect oldConnect = new();
        Connect newConnect = new();
        Guid unitId = Guid.NewGuid();
        frame.AddConnect(oldConnect, unitId);
        frame.OnReceiveMessage(new General_FrameStateData
        {
            sceneVersion = 1,
            data = new NetworkFrameData
            {
                frameId = 10,
                datas = new List<NetworkStateData>
                {
                    new NetworkPlayerInputStateData { unitId = unitId, moveX = 1f },
                    new NetworkSkillChainSelectData { unitId = unitId, skillChainIndex = 4 },
                    new NetworkCharacterEditData { unitId = unitId, characterData = new CharacterData() },
                },
            },
        }, oldConnect);
        frame.RemoveConnect(oldConnect);
        frame.AddConnect(newConnect, unitId);
        frame.OnReceiveMessage(new General_FrameStateData
        {
            sceneVersion = 1,
            data = new NetworkFrameData
            {
                frameId = 10,
                datas = new List<NetworkStateData>
                {
                    new NetworkSkillChainSelectData { unitId = unitId, skillChainIndex = 2 },
                },
            },
        }, newConnect);
        Queue<NetworkState> received = null;
        frame.onHandleReceive = (_, states) => received = states;
        frame.HandleReceive();
        Assert.That(received.Count, Is.EqualTo(1));
        Assert.That(((NetworkSkillChainSelectData)received.Dequeue().data).skillChainIndex, Is.EqualTo(2));
        frame.Stop();
    }

    [Test]
    public void SceneResetPreservesWorldAndRegistriesButClearsRuntimeState()
    {
        using World world = new("Retained battle world test");
        EntityManager manager = world.EntityManager;
        Entity oldPlayer = manager.CreateEntity(typeof(DungeonRuntimeOwnedEntity));
        Entity disabledUnit = manager.CreateEntity(typeof(DungeonRuntimeOwnedEntity), typeof(Disabled));
        Entity prefab = manager.CreateEntity(typeof(DungeonRuntimeOwnedEntity), typeof(Prefab));
        Entity variables = manager.CreateEntity(typeof(WorldVariableComponent));
        manager.AddBuffer<WorldVariableElement>(variables).Add(default);
        Entity frameEntity = manager.CreateEntity();
        FrameReceiveBufferComponent frames = new();
        frames.frames.Add(100, new Queue<NetworkState>());
        manager.AddComponentObject(frameEntity, frames);
        EffectDataBridgeComponent bridge = EffectDataBridgeUtility.GetOrCreate(manager);
        bridge.Values.Add(1, new EffectDataList(Array.Empty<CrystalMagic.Game.Data.Effects.EffectData>()));
        bridge.Values.Add(2, new EffectDataList(Array.Empty<CrystalMagic.Game.Data.Effects.EffectData>()));
        bridge.RegistryIds.Add(1);

        BattleSceneResetUtility.Reset(world);

        Assert.That(world.IsCreated, Is.True);
        Assert.That(manager.Exists(oldPlayer), Is.False);
        Assert.That(manager.Exists(disabledUnit), Is.False);
        Assert.That(manager.Exists(prefab), Is.True);
        Assert.That(manager.Exists(variables), Is.True);
        Assert.That(manager.GetBuffer<WorldVariableElement>(variables).Length, Is.Zero);
        Assert.That(frames.frames, Is.Empty);
        Assert.That(bridge.Values.ContainsKey(1), Is.True);
        Assert.That(bridge.Values.ContainsKey(2), Is.False);
    }

    [Test]
    public void TransitionReadyLocksAllInputWithoutRemovingCollision()
    {
        using World world = new("Transition ready input lock test");
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity(typeof(PlayerInputComponent), typeof(PhysicsCollider));
        manager.SetComponentData(player, new PlayerInputComponent
        {
            Move = new float2(1f, 1f),
            IsPrimaryHeld = 1,
            IsInteractHeld = 1,
            IsInventoryHeld = 1,
        });
        BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent
        {
            LifeState = BattlePlayerLifeState.Alive,
            ConnectionState = BattlePlayerConnectionState.Online,
            TransitionReady = 1,
        });

        BattlePlayerStatusComponent status = manager.GetComponentData<BattlePlayerStatusComponent>(player);
        PlayerInputComponent input = manager.GetComponentData<PlayerInputComponent>(player);
        Assert.That(status.IsWaitingForTransition, Is.True);
        Assert.That(status.IsFaded, Is.True);
        Assert.That(input.Move, Is.EqualTo(float2.zero));
        Assert.That(input.IsPrimaryHeld, Is.Zero);
        Assert.That(input.IsInteractHeld, Is.Zero);
        Assert.That(input.IsInventoryHeld, Is.Zero);
        Assert.That(manager.HasComponent<PhysicsCollider>(player), Is.True);
    }

    [Test]
    public void FailedRecoveryDoesNotEnterTownOrClearSettlement()
    {
        ClientBattleManager manager = new();
        typeof(ClientBattleManager).GetField("pendingSettlement", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, true);
        TransitionData transition = OnlineBattlePreparationState.CreateReturnToTownTransitionData(manager);
        transition.OnLoadFailed("test load failure");
        Assert.That(transition.TargetStateType, Is.EqualTo(typeof(OnlineBattleRecoveryState)));
        Assert.That(((BattleRecoveryContext)transition.TargetStateData).Manager, Is.SameAs(manager));
        Assert.That(manager.HasPendingSettlement, Is.True);
    }

    [Test]
    public void SceneResetClearsSkillSelectionFrameBoundary()
    {
        ClientFrameManager frame = new();
        Assert.That(frame.ShouldApplySkillChainState(37), Is.True);
        frame.ClearOrders();
        Assert.That(frame.ShouldApplySkillChainState(0), Is.True);
        Assert.That(frame.ShouldApplySkillChainState(1), Is.True);
    }

    [Test]
    public void InitialSpawnSnapshotContainsTransferredHealthAndMana()
    {
        using World world = new("Transfer snapshot test");
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(UnitVitalityComponent), typeof(UnitManaComponent));
        Guid unitId = Guid.NewGuid();
        manager.SetComponentData(player, new NetworkIdentityComponent { id = unitId });
        manager.SetComponentData(player, new UnitVitalityComponent { CurrentHealth = 42f });
        manager.SetComponentData(player, new UnitManaComponent { CurrentMana = 17f });
        manager.AddComponentObject(player, new NetworkEntitySpawnInfoComponent
        {
            entityInfo = new NetworkEntitySpawnInfo { unitId = unitId, prefabName = "PlayerDungeon" },
        });
        NetworkEntitySpawnInfo[] infos = NetworkEntitySpawnUtility.CreateSnapshotInfos(manager);
        Assert.That(infos.Length, Is.EqualTo(1));
        Assert.That(infos[0].hasHealth, Is.True);
        Assert.That(infos[0].hasMana, Is.True);
        Assert.That(infos[0].health, Is.EqualTo(42f));
        Assert.That(infos[0].mana, Is.EqualTo(17f));
    }

    [Test]
    public void OfflineDisablesActionsAndCollisionWithoutKillingPlayer()
    {
        using World world = new("Battle spectator test");
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity(typeof(PlayerInputComponent), typeof(PhysicsCollider));
        manager.SetComponentData(player, new PlayerInputComponent
        {
            Move = new float2(1f, 0f), IsPrimaryHeld = 1, IsInteractHeld = 1, SkillChainIndex = 3,
        });
        BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent
        {
            LifeState = BattlePlayerLifeState.Alive,
            ConnectionState = BattlePlayerConnectionState.Offline,
        });
        Assert.That(manager.HasComponent<BattleSpectatorComponent>(player), Is.True);
        Assert.That(manager.HasComponent<PhysicsCollider>(player), Is.False);
        PlayerInputComponent input = manager.GetComponentData<PlayerInputComponent>(player);
        Assert.That(input.Move, Is.EqualTo(float2.zero));
        Assert.That(input.IsPrimaryHeld, Is.Zero);
        Assert.That(input.IsInteractHeld, Is.Zero);
        Assert.That(input.SkillChainIndex, Is.EqualTo(3));

        BattlePlayerStatusUtility.Apply(manager, player, new BattlePlayerStatusComponent());
        Assert.That(manager.HasComponent<BattleSpectatorComponent>(player), Is.False);
        Assert.That(manager.HasComponent<PhysicsCollider>(player), Is.True);
    }

    [Test]
    public void DeadPlayerStaysSpectatorAfterReconnecting()
    {
        using World world = new("Dead player reload test");
        EntityManager manager = world.EntityManager;
        Entity player = manager.CreateEntity(typeof(PlayerInputComponent));
        BattlePlayerStatusComponent status = new()
        {
            LifeState = BattlePlayerLifeState.Dead,
            ConnectionState = BattlePlayerConnectionState.Offline,
        };
        BattlePlayerStatusUtility.Apply(manager, player, status);
        status.ConnectionState = BattlePlayerConnectionState.Online;
        BattlePlayerStatusUtility.Apply(manager, player, status);
        Assert.That(manager.HasComponent<BattleSpectatorComponent>(player), Is.True);
        Assert.That(manager.GetComponentData<BattlePlayerStatusComponent>(player).LifeState,
            Is.EqualTo(BattlePlayerLifeState.Dead));
    }

    [Test]
    public void OnlineSpectatorCanMoveButOfflineSpectatorCannot()
    {
        using World world = new("Spectator input test");
        EntityManager manager = world.EntityManager;
        Entity worldContext = manager.CreateEntity(typeof(GameWorldContextComponent));
        manager.SetComponentData(worldContext, new GameWorldContextComponent { Role = GameWorldRole.Server });
        Entity player = manager.CreateEntity(typeof(PlayerInputComponent), typeof(NetworkIdentityComponent));
        Guid id = Guid.NewGuid();
        manager.SetComponentData(player, new NetworkIdentityComponent { id = id });
        BattlePlayerStatusComponent status = new() { LifeState = BattlePlayerLifeState.Dead };
        BattlePlayerStatusUtility.Apply(manager, player, status);
        NetworkStateApplyContext context = new(manager, 10, 33);
        NetworkPlayerInputStateData input = new() { unitId = id, moveX = 1f, isPrimaryHeld = 1 };
        input.Apply(context);
        Assert.That(manager.GetComponentData<PlayerInputComponent>(player).Move.x, Is.EqualTo(1f));
        Assert.That(manager.GetComponentData<PlayerInputComponent>(player).IsPrimaryHeld, Is.Zero);

        status.ConnectionState = BattlePlayerConnectionState.Offline;
        BattlePlayerStatusUtility.Apply(manager, player, status);
        input.Apply(context);
        Assert.That(manager.GetComponentData<PlayerInputComponent>(player).Move, Is.EqualTo(float2.zero));
    }

    [Test]
    public void OldThemeFramesCannotEnterNewThemeQueue()
    {
        FrameManager manager = new() { sceneVersion = 2 };
        General_FrameStateData message = new()
        {
            sceneVersion = 1,
            data = new NetworkFrameData
            {
                frameId = 3,
                datas = new List<NetworkStateData> { new NetworkPlayerInputStateData() },
            },
        };
        manager.OnReceiveMessage(message, null);
        Assert.That(manager.receivedOrder, Is.Empty);
        message.sceneVersion = 2;
        manager.OnReceiveMessage(message, null);
        Assert.That(manager.receivedOrder[3].Count, Is.EqualTo(1));
    }

    [Test]
    public void OldThemePongDoesNotChangeNewThemeClock()
    {
        ClientFrameManager manager = new() { sceneVersion = 2 };
        manager.ReceiveClockSample(new B2C_FramePong
        {
            sceneVersion = 1, clientSendTime = 100, running = true, serverFrame = 500,
        }, 150);
        Assert.That(manager.SmoothedRttMs, Is.Zero);
        Assert.That(manager.HasFreshServerClock(150), Is.False);
    }

    [Test]
    public void BattleExitNodeAndStatusRoundTripThroughProtocol()
    {
        NPCInteractionNodeDataFactory factory = new();
        NPCInteractionNodeDataRegistry.RegisterAll(factory);
        Assert.That(NPCInteractionNodeDataRegistry.TryGetNodeType("RequestBattleExit", out Type nodeType), Is.True);
        NPCRequestBattleExitInteractionNodeData node = (NPCRequestBattleExitInteractionNodeData)Activator.CreateInstance(nodeType);
        Assert.That(node.ExecutionTargets, Is.EqualTo(GameWorldExecutionTarget.Client));

        NetworkBattlePlayerStatusStateData state = new()
        {
            unitId = Guid.NewGuid(), lifeState = BattlePlayerLifeState.Dead,
            connectionState = BattlePlayerConnectionState.Online, transitionReady = 1,
        };
        JsonSerializerSettings settings = new() { TypeNameHandling = TypeNameHandling.Auto };
        List<NetworkStateData> source = new() { state };
        List<NetworkStateData> decoded = JsonConvert.DeserializeObject<List<NetworkStateData>>(
            JsonConvert.SerializeObject(source, settings), settings);
        NetworkBattlePlayerStatusStateData copy = (NetworkBattlePlayerStatusStateData)decoded[0];
        Assert.That(copy.unitId, Is.EqualTo(state.unitId));
        Assert.That(copy.lifeState, Is.EqualTo(BattlePlayerLifeState.Dead));
        Assert.That(copy.transitionReady, Is.EqualTo(1));
    }
}
