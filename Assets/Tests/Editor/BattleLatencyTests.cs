using System;
using System.Collections.Generic;
using NUnit.Framework;
using Server;
using Unity.Entities;

public sealed class BattleLatencyTests
{
    [Test]
    public void HealthPresentationUpdatesOnReceiptWithoutAdvancingSimulation()
    {
        Guid id = Guid.NewGuid();
        var connection = new Connect();
        var client = new ClientFrameManager { connect = connection, currentFrame = 20, sceneVersion = 3 };
        int applied = 0;
        client.onHandleReceive += (_, _) => applied++;
        client.OnReceiveMessage(Packet(19, 3, id, 75), connection);
        Assert.That(client.TryGetPresentedHealth(id, out float health), Is.True);
        Assert.That(health, Is.EqualTo(75));
        Assert.That(applied, Is.Zero);
        Assert.That(client.currentFrame, Is.EqualTo(20));
        Assert.That(client.receivedOrder.ContainsKey(19), Is.True);

        var damaged = new HashSet<Guid>();
        client.ConsumePresentedDamageUnits(damaged);
        Assert.That(damaged, Is.EquivalentTo(new[] { id }));
        client.ConsumePresentedDamageUnits(damaged);
        Assert.That(damaged, Is.Empty);
        client.HandleReceive();
        Assert.That(applied, Is.EqualTo(1));
    }

    [Test]
    public void StaleHealthWrongSceneAndWrongConnectionCannotReplacePresentation()
    {
        Guid id = Guid.NewGuid();
        var connection = new Connect();
        var client = new ClientFrameManager { connect = connection, sceneVersion = 3 };
        client.OnReceiveMessage(Packet(20, 3, id, 50), connection);
        client.OnReceiveMessage(Packet(19, 3, id, 100), connection);
        client.OnReceiveMessage(Packet(21, 2, id, 100), connection);
        client.OnReceiveMessage(Packet(21, 3, id, 100), new Connect());
        Assert.That(client.TryGetPresentedHealth(id, out float health), Is.True);
        Assert.That(health, Is.EqualTo(50));
        var staleSpawn = Packet(18, 3, id, 100);
        staleSpawn.frames[0].datas.Insert(0, new NetworkEntitySpawnStateData { unitId = id });
        client.OnReceiveMessage(staleSpawn, connection);
        Assert.That(client.TryGetPresentedHealth(id, out health), Is.True);
        Assert.That(health, Is.EqualTo(50));
        // Separate world updates can legitimately produce two changes at one tick.
        client.OnReceiveMessage(Packet(20, 3, id, 25), connection);
        Assert.That(client.TryGetPresentedHealth(id, out health), Is.True);
        Assert.That(health, Is.EqualTo(25));
        client.sceneVersion++;
        Assert.That(client.TryGetPresentedHealth(id, out _), Is.False);
        client.ClearOrders();
        var damaged = new HashSet<Guid>();
        client.ConsumePresentedDamageUnits(damaged);
        Assert.That(damaged, Is.Empty);
    }

    [Test]
    public void DespawnAndSceneResetDiscardPresentedHealth()
    {
        Guid id = Guid.NewGuid();
        var connection = new Connect();
        var client = new ClientFrameManager { connect = connection, sceneVersion = 3 };
        var packet = Packet(20, 3, id, 50);
        client.OnReceiveMessage(packet, connection);
        packet.frames[0].datas = new() { new NetworkEntityDespawnStateData { unitId = id } };
        client.OnReceiveMessage(packet, connection);
        Assert.That(client.TryGetPresentedHealth(id, out _), Is.False);
        var damaged = new HashSet<Guid>();
        client.ConsumePresentedDamageUnits(damaged);
        Assert.That(damaged, Is.Empty);
        client.OnReceiveMessage(Packet(21, 3, id, 40), connection);
        client.ClearOrders();
        Assert.That(client.TryGetPresentedHealth(id, out _), Is.False);
    }

    [Test]
    public void LocalBattleFramesArriveBeforeTransportUpdateAndRemainSerialized()
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(42, out var serverTransport, out var clientTransport);
        Connect accepted = null;
        serverTransport.OnAccept += connection => accepted = connection;
        try
        {
            serverTransport.Init(); clientTransport.Init();
            clientTransport.Connect(serverTransport.LocalEndpoint, out Connect connection);
            for (int index = 0; index < 4; index++) { clientTransport.Update(); serverTransport.Update(); }
            Assert.That(accepted, Is.Not.Null);
            Guid id = Guid.NewGuid();
            var client = new ClientFrameManager { connect = connection, currentFrame = 10, sceneVersion = 3 };
            connection.RegisterCallback(MessageCodec.GetOpcode<General_FrameStateData>(), client.OnReceiveMessage);
            var packet = Packet(9, 3, id, 75);
            accepted.Send(packet);
            ((NetworkVitalityStateData)packet.frames[0].datas[0]).currentHealth = 1;
            Assert.That(client.TryGetPresentedHealth(id, out float health), Is.True);
            Assert.That(health, Is.EqualTo(75));
            Assert.That(client.receivedOrder[9].Count, Is.EqualTo(1));
            clientTransport.Update(); serverTransport.Update();
            Assert.That(client.receivedOrder[9].Count, Is.EqualTo(1), "Immediate packet must not also remain queued.");

            client.ReceiveClockSample(new B2C_FramePong { sceneVersion = 3, clientSendTime = 1, serverFrame = 9, running = true }, 201);
            client.UpdateSimulationSpeed(201);
            Assert.That(client.TargetAheadFrames, Is.EqualTo(1));
            Assert.That(client.EstimatedServerFrame, Is.EqualTo(9));
        }
        finally { clientTransport.Shutdown(); serverTransport.Shutdown(); }
    }

    [Test]
    public void LocalBattleFramesNeverOvertakeControlMessages()
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(42, out var server, out var client);
        Connect accepted = null;
        server.OnAccept += connection => accepted = connection;
        try
        {
            server.Init(); client.Init();
            client.Connect(server.LocalEndpoint, out Connect connection);
            for (int index = 0; index < 4; index++) { client.Update(); server.Update(); }
            var order = new List<string>();
            connection.RegisterCallback(MessageCodec.GetOpcode<S2C_Pong>(), (_, _) => order.Add("control"));
            connection.RegisterCallback(MessageCodec.GetOpcode<General_FrameStateData>(), (_, _) => order.Add("frame"));
            accepted.Send(new S2C_Pong());
            accepted.Send(Packet(1, 0, Guid.NewGuid(), 75));
            Assert.That(order, Is.Empty);
            server.Update(); client.Update();
            Assert.That(order, Is.EqualTo(new[] { "control", "frame" }));
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }

    [Test]
    public void WorldPassCapturesPlayerDamageAndFlushesItWithoutTimerDelay()
    {
        using World world = new("Post-player damage collection");
        var manager = world.EntityManager;
        var frame = new ServerFrameManager { running = true, currentFrame = 7 };
        manager.AddComponentObject(manager.CreateEntity(), new FrameManagerComponent { manager = frame });
        manager.SetComponentData(manager.CreateEntity(typeof(BattleSimulationScope)),
            new BattleSimulationScope { Pass = BattleSimulationPass.World });
        Entity player = manager.CreateEntity(typeof(NetworkIdentityComponent), typeof(PlayerInputComponent), typeof(UnitVitalityComponent));
        manager.SetComponentData(player, new NetworkIdentityComponent { id = Guid.NewGuid() });
        manager.SetComponentData(player, new UnitVitalityComponent { BaseMaxHealth = 100, CurrentHealth = 75, NetworkDirty = 1 });
        world.GetOrCreateSystemManaged<ServerNetworkStateCollectSystem>().Update();
        Assert.That(frame.sendOrder[6].Count, Is.EqualTo(1));
        Assert.That(frame.FlushNetwork(1), Is.True);
        Assert.That(frame.sendOrder, Is.Empty);
    }

    private static General_FrameStateData Packet(uint frame, uint scene, Guid id, float health) => new()
    {
        sceneVersion = scene,
        frames = new() { new() { frameId = frame, datas = new()
        {
            new NetworkVitalityStateData { unitId = id, baseMaxHealth = 100, currentHealth = health },
        } } },
    };
}
