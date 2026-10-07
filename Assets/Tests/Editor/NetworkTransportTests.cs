using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Server;

public sealed class NetworkTransportTests
{
    [Test]
    public void MessageConnectionWorksWithoutTcpOrIpEndpoints()
    {
        MessageCodec.Init();
        RecordingTransport sender = new();
        using Connect outgoing = new();
        outgoing.Init(sender);
        using Connect incoming = new();
        incoming.Init(new RecordingTransport());
        List<long> received = new();
        incoming.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), (message, connection) =>
        {
            Assert.That(connection, Is.SameAs(incoming));
            received.Add(((C2S_Ping)message).Time);
        });

        outgoing.Send(new C2S_Ping { Time = 11 });
        outgoing.Send(new C2S_Ping { Time = 22 });
        Assert.That(sender.Messages.Count, Is.EqualTo(2));
        Assert.That(outgoing.RemoteEndpoint.ToString(), Is.EqualTo("test-peer"));
        foreach (byte[] packet in sender.Messages)
            Assert.That(incoming.TryReceive(packet, out _), Is.True);
        CollectionAssert.AreEqual(new long[] { 11, 22 }, received);
    }

    [Test]
    public void TcpFramingPreservesWireFormatAcrossPartialAndCombinedReads()
    {
        byte[] message = MessageCodec.Encode(0x1234, new byte[] { 0x7b, 0x7d });
        byte[] packet = TCPPacketCode.Pack(message);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 4, 0x12, 0x34, 0x7b, 0x7d }, packet);
        using MemoryStream stream = new();
        stream.Write(packet, 0, 3);
        Assert.That(TCPPacketCode.TryUnPack(stream, out _, out _), Is.EqualTo(PacketReadResult.NeedMoreData));
        stream.Write(packet, 3, 2);
        Assert.That(TCPPacketCode.TryUnPack(stream, out _, out _), Is.EqualTo(PacketReadResult.NeedMoreData));
        stream.Write(packet, 5, packet.Length - 5);
        stream.Write(packet, 0, packet.Length);
        for (int i = 0; i < 2; i++)
        {
            Assert.That(TCPPacketCode.TryUnPack(stream, out byte[] decoded, out _), Is.EqualTo(PacketReadResult.Success));
            CollectionAssert.AreEqual(message, decoded);
        }
        Assert.That(stream.Length, Is.Zero);
    }

    [Test]
    public void MessageTransportUsesTheSameDecodeAndHandlerFailureReasons()
    {
        MessageCodec.Init();
        using Connect connection = new();
        connection.Init(new RecordingTransport());
        Assert.That(connection.TryReceive(MessageCodec.Encode(ushort.MaxValue, Array.Empty<byte>()), out DisconnectInfo unknown), Is.False);
        Assert.That(unknown.Reason, Is.EqualTo(DisconnectReason.UnknownOpcode));
        Assert.That(connection.TryReceive(new byte[] { 1 }, out DisconnectInfo truncated), Is.False);
        Assert.That(truncated.Reason, Is.EqualTo(DisconnectReason.DeserializeError));
        connection.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), (_, _) => throw new InvalidOperationException("handler failed"));
        Assert.That(connection.TryReceive(MessageCodec.Encode(new C2S_Ping()), out DisconnectInfo handler), Is.False);
        Assert.That(handler.Reason, Is.EqualTo(DisconnectReason.HandlerError));
        Assert.That(handler.Exception.Message, Is.EqualTo("handler failed"));
    }

    [Test]
    public void TcpServicesExchangeMessagesAndFlushBeforeDisconnectThroughInterfaces()
    {
        MessageCodec.Init();
        IServerTransport server = new ServerService(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
        IClientTransport client = new ClientService();
        int connected = 0;
        int serverDisconnected = 0;
        int clientDisconnected = 0;
        int responses = 0;
        List<long> received = new();
        Connect accepted = null;
        server.OnAccept += connection =>
        {
            accepted = connection;
            connection.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), (message, _) =>
            {
                received.Add(((C2S_Ping)message).Time);
                if (received.Count == 2)
                    server.DisconnectAfterSend(connection);
            });
        };
        server.OnDisconnected += _ => serverDisconnected++;
        client.OnDisconnected += _ => clientDisconnected++;
        try
        {
            server.Init();
            client.Init();
            client.Connect(server.LocalEndpoint, out Connect connection);
            connection.OnConnected += _ => connected++;
            connection.RegisterCallback(MessageCodec.GetOpcode<S2C_Pong>(), (_, _) => responses++);
            Assert.That(connection.State, Is.EqualTo(ConnectState.Pending));
            Assert.That(connected, Is.Zero);
            PumpUntil(server, client, () => connected == 1 && accepted != null);
            connection.Send(new C2S_Ping { Time = 11 });
            connection.Send(new C2S_Ping { Time = 22 });
            PumpUntil(server, client, () => clientDisconnected == 1);

            CollectionAssert.AreEqual(new long[] { 11, 22 }, received);
            Assert.That(responses, Is.EqualTo(2));
            Assert.That(serverDisconnected, Is.EqualTo(1));
            Assert.That(accepted.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.CloseAfterSend));
            Assert.That(connection.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
            Assert.That(connection.State, Is.EqualTo(ConnectState.Close));
        }
        finally
        {
            client.Shutdown();
            server.Shutdown();
        }
    }

    [Test]
    public void PendingConnectionCanBeCancelledBeforeTheFirstUpdate()
    {
        IClientTransport client = new ClientService();
        int disconnected = 0;
        int connected = 0;
        int failed = 0;
        try
        {
            client.Init();
            client.OnDisconnected += _ => disconnected++;
            client.OnConnectedFail += _ => failed++;
            client.Connect(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 1)), out Connect connection);
            connection.OnConnected += _ => connected++;
            client.Disconnect(connection);
            client.Disconnect(connection);
            client.Update();
            client.Update();
            Assert.That(disconnected, Is.EqualTo(1));
            Assert.That(connected, Is.Zero);
            Assert.That(failed, Is.Zero);
            Assert.That(connection.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.LocalClose));
        }
        finally
        {
            client.Shutdown();
        }
    }

    [Test]
    public void ShutdownReleasesPendingConnectionsAndListeningPortAndCanRepeat()
    {
        IServerTransport server = new ServerService(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
        IClientTransport client = new ClientService();
        int disconnected = 0;
        try
        {
            server.Init();
            client.Init();
            client.OnDisconnected += _ => disconnected++;
            client.Connect(server.LocalEndpoint, out Connect pending);
            client.Shutdown();
            server.Shutdown();
            Assert.DoesNotThrow(client.Shutdown);
            Assert.DoesNotThrow(server.Shutdown);
            Assert.That(pending.State, Is.EqualTo(ConnectState.Close));
            Assert.That(disconnected, Is.Zero);
            using Socket probe = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Assert.Throws<SocketException>(() => probe.Connect(((TcpEndpoint)server.LocalEndpoint).Address));
        }
        finally
        {
            client.Shutdown();
            server.Shutdown();
        }
    }

    private static void PumpUntil(IServerTransport server, IClientTransport client, Func<bool> done)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!done() && clock.ElapsedMilliseconds < 3000)
        {
            server.Update();
            client.Update();
            Thread.Sleep(1);
        }
        Assert.That(done(), Is.True, "TCP loopback did not complete in 3 seconds.");
    }

    [Test]
    public void TcpShutdownInsideConnectingCallbackDoesNotContinueOrDispatch()
    {
        ClientService client = new();
        client.Init();
        client.OnConnecting += _ => client.Shutdown();
        client.Connect(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 1)), out Connect connect);
        int connected = 0;
        connect.OnConnected += _ => connected++;
        Assert.DoesNotThrow(client.Update);
        Assert.DoesNotThrow(client.Update);
        Assert.That(connect.State, Is.EqualTo(ConnectState.Close));
        Assert.That(connect.TrySend(new C2S_Ping()), Is.False);
        Assert.That(connected, Is.Zero);
    }

    [Test]
    public void TcpReceiveCallbackCanShutdownWithSeveralConnections()
    {
        MessageCodec.Init();
        ServerService server = new(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
        ClientService client = new();
        int accepted = 0;
        int received = 0;
        server.OnAccept += connect =>
        {
            accepted++;
            connect.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), (_, _) => { received++; server.Shutdown(); });
        };
        try
        {
            server.Init(); client.Init();
            client.Connect(server.LocalEndpoint, out Connect a);
            client.Connect(server.LocalEndpoint, out Connect b);
            PumpUntil(server, client, () => accepted == 2 && a.State == ConnectState.Connected && b.State == ConnectState.Connected);
            a.Send(new C2S_Ping()); b.Send(new C2S_Ping());
            PumpUntil(server, client, () => received > 0);
            Assert.That(received, Is.EqualTo(1));
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }

    [Test]
    public void LoopbackIsQueuedAndFlushesBeforeClosing()
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(42, out LoopbackTransport server, out LoopbackTransport client);
        List<long> received = new();
        Connect accepted = null;
        int disconnected = 0;
        try
        {
            server.OnAccept += connect => accepted = connect;
            server.Init(); client.Init();
            client.Connect(server.LocalEndpoint, out Connect connect);
            connect.RegisterCallback(MessageCodec.GetOpcode<S2C_Pong>(), (message, _) => received.Add(((S2C_Pong)message).Time));
            connect.OnDisconnected += _ => disconnected++;
            Assert.That(accepted, Is.Null);
            PumpUntil(server, client, () => accepted != null && connect.State == ConnectState.Connected);
            accepted.Send(new S2C_Pong { Time = 7 });
            accepted.Send(new S2C_Pong { Time = 9 });
            Assert.That(received.Count, Is.Zero);
            server.DisconnectAfterSend(accepted);
            PumpUntil(server, client, () => disconnected == 1);
            CollectionAssert.AreEqual(new long[] { 7, 9 }, received);
            Assert.That(accepted.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.CloseAfterSend));
            Assert.DoesNotThrow(client.Update);
            Assert.That(disconnected, Is.EqualTo(1));
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }

    [Test]
    public void LoopbackShutdownAndCancelDuringConnectAreIdempotent()
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(42, out LoopbackTransport server, out LoopbackTransport client);
        try
        {
            server.Init(); client.Init();
            int disconnected = 0;
            client.OnDisconnected += _ => disconnected++;
            client.Connect(server.LocalEndpoint, out Connect connect);
            client.Disconnect(connect); client.Disconnect(connect);
            client.Update(); server.Update(); client.Update();
            Assert.That(disconnected, Is.EqualTo(1));
            client.Connect(server.LocalEndpoint, out Connect second);
            client.Update();
            server.Shutdown();
            second.startTime = NetworkTimer.Instance.TimeNow - ServerUtility.ConnectTimeout;
            client.Update();
            Assert.That(second.State, Is.EqualTo(ConnectState.Close));
            Assert.That(disconnected, Is.EqualTo(2));
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }

    [Test]
    public void LocalPeersSurviveMainThreadSilenceLongerThanTheNetworkHeartbeatTimeout()
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(42, out LoopbackTransport server, out LoopbackTransport client);
        Connect accepted = null;
        server.OnAccept += connection => accepted = connection;
        try
        {
            server.Init(); client.Init();
            client.Connect(server.LocalEndpoint, out Connect connect);
            PumpUntil(server, client, () => accepted != null && connect.State == ConnectState.Connected);
            long beforeLoading = NetworkTimer.Instance.TimeNow - ServerUtility.Timeout - 20_000;
            accepted.LastReceiveTime = connect.LastReceiveTime = beforeLoading;
            for (int index = 0; index < 3; index++) { server.Update(); client.Update(); }
            Assert.That(accepted.State, Is.EqualTo(ConnectState.Connected));
            Assert.That(connect.State, Is.EqualTo(ConnectState.Connected));
            Assert.That(connect.LastDisconnectInfo, Is.Null);

            int received = 0;
            connect.RegisterCallback(MessageCodec.GetOpcode<S2C_Pong>(), (_, _) => received++);
            accepted.Send(new S2C_Pong { Time = 42 });
            PumpUntil(server, client, () => received > 0);
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LocalPeerClosureStillPropagatesAfterAStall(bool shutdown)
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(42, out LoopbackTransport server, out LoopbackTransport client);
        Connect accepted = null;
        int disconnected = 0;
        server.OnAccept += connection => accepted = connection;
        client.OnDisconnected += _ => disconnected++;
        try
        {
            server.Init(); client.Init();
            client.Connect(server.LocalEndpoint, out Connect connect);
            PumpUntil(server, client, () => accepted != null && connect.State == ConnectState.Connected);
            accepted.LastReceiveTime = connect.LastReceiveTime = NetworkTimer.Instance.TimeNow - ServerUtility.Timeout;
            if (shutdown) server.Shutdown();
            else { server.Disconnect(accepted); server.Update(); }
            client.Update(); client.Update();
            Assert.That(connect.State, Is.EqualTo(ConnectState.Close));
            Assert.That(connect.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
            Assert.That(connect.LastDisconnectInfo.Phase, Is.EqualTo("LoopbackPeerClosed"));
            Assert.That(disconnected, Is.EqualTo(1));
        }
        finally { client.Shutdown(); server.Shutdown(); }
    }

    [Test]
    public void NetworkMessageTransportStillDisconnectsOnARealHeartbeatTimeout()
    {
        MessageCodec.Init();
        StalledTransport transport = new();
        int disconnected = 0;
        try
        {
            transport.Init();
            transport.OnDisconnected += _ => disconnected++;
            transport.Connect(new TestEndpoint(), out Connect connect);
            transport.MakeReady(connect);
            transport.Update();
            Assert.That(connect.State, Is.EqualTo(ConnectState.Connected));
            connect.LastReceiveTime = NetworkTimer.Instance.TimeNow - ServerUtility.Timeout;
            transport.Update();
            Assert.That(connect.State, Is.EqualTo(ConnectState.Close));
            Assert.That(connect.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.Timeout));
            Assert.That(connect.LastDisconnectInfo.Phase, Is.EqualTo("HeartbeatTimeout"));
            Assert.That(disconnected, Is.EqualTo(1));
        }
        finally { transport.Shutdown(); }
    }

    [Test]
    public void MessageTransportTimeoutAndQueueOverflowAlwaysRelease()
    {
        MessageCodec.Init();
        StalledTransport transport = new();
        int disconnected = 0;
        transport.Init();
        transport.OnDisconnected += _ => disconnected++;
        transport.Connect(new TestEndpoint(), out Connect pending);
        pending.startTime = NetworkTimer.Instance.TimeNow - ServerUtility.ConnectTimeout;
        transport.Update();
        Assert.That(pending.State, Is.EqualTo(ConnectState.Close));
        Assert.That(pending.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.Timeout));
        Assert.That(disconnected, Is.EqualTo(1));
        transport.Connect(new TestEndpoint(), out Connect congested);
        transport.MakeReady(congested);
        transport.Update();
        byte[] packet = new byte[MessageCodec.MaxMessageLength];
        transport.Enqueue(congested, packet);
        transport.Enqueue(congested, packet);
        transport.Enqueue(congested, packet);
        transport.Enqueue(congested, packet);
        Assert.That(congested.TrySend(new C2S_Ping()), Is.False);
        transport.Update();
        Assert.That(disconnected, Is.EqualTo(2));
        Assert.That(transport.Released, Is.EqualTo(2));
        transport.Shutdown();
    }

    [Test]
    public void MessageTransportDrainDeadlineCannotBeExtendedByRepeatedRequests()
    {
        MessageCodec.Init();
        StalledTransport transport = new();
        transport.Init();
        transport.Connect(new TestEndpoint(), out Connect connect);
        transport.MakeReady(connect);
        transport.Update();
        connect.Send(new C2S_Ping());
        transport.DisconnectAfterSend(connect);
        transport.ExpireDrain(connect);
        transport.DisconnectAfterSend(connect);
        transport.Update();
        Assert.That(connect.State, Is.EqualTo(ConnectState.Close));
        Assert.That(connect.LastDisconnectInfo.Phase, Is.EqualTo("DrainTimeout"));
        Assert.That(transport.Released, Is.EqualTo(1));
        transport.Shutdown();
    }

    [Test]
    public void SteamFragmentsRoundTripMaximumMessageAndRejectInvalidSequences()
    {
        byte[] message = new byte[MessageCodec.MaxMessageLength];
        for (int i = 0; i < message.Length; i++) message[i] = (byte)i;
        ReliableMessageFragments receiver = new();
        byte[] completed = null;
        for (int offset = 0; offset < message.Length;)
        {
            byte[] fragment = ReliableMessageFragments.Pack(message, offset);
            Assert.That(receiver.TryReceive(fragment, 0, out completed), Is.True);
            offset += fragment.Length - ReliableMessageFragments.HeaderLength;
        }
        CollectionAssert.AreEqual(message, completed);
        byte[] first = ReliableMessageFragments.Pack(message, 0);
        Assert.That(receiver.TryReceive(first, 0, out _), Is.True);
        Assert.That(receiver.TryReceive(first, 0, out _), Is.False, "Duplicate fragment must fail.");
        Assert.That(receiver.IsExpired(ServerUtility.Timeout), Is.True);
        receiver.Clear();
        first[4] = 0x7f;
        Assert.That(receiver.TryReceive(first, 0, out _), Is.False, "Huge allocation must be rejected before allocating.");
        Assert.That(receiver.TryReceive(new byte[2], 0, out _), Is.False);
    }

    private sealed class StalledTransport : MessageTransport
    {
        public int Released;
        public void MakeReady(Connect connect) => peers[connect].Ready = true;
        public void Enqueue(Connect connect, byte[] packet) => peers[connect].Send(packet);
        public void ExpireDrain(Connect connect) => peers[connect].DrainDeadline = -1;
        protected override void Open() { }
        protected override void BeginConnect(Peer peer) { }
        protected override void Poll() { }
        protected override bool TrySendPacket(Peer peer, byte[] packet) => false;
        protected override void ClosePeer(Peer peer) => Released++;
        protected override void Close() { }
    }

    [Test]
    public void CodecRejectsArbitraryClrTypesAndExcessiveDepth()
    {
        MessageCodec.Init();
        byte[] json = System.Text.Encoding.UTF8.GetBytes("{\"$type\":\"System.Windows.Data.ObjectDataProvider, PresentationFramework\"}");
        Assert.That(MessageCodec.TryDecode(MessageCodec.Encode(MessageCodec.GetOpcode<C2S_Ping>(), json), out _, out _, out _),
            Is.EqualTo(MessageDecodeResult.InvalidPayload));
        string deep = new string('[', 100) + "0" + new string(']', 100);
        Assert.That(MessageCodec.TryDecode(MessageCodec.Encode(MessageCodec.GetOpcode<C2S_Ping>(), System.Text.Encoding.UTF8.GetBytes(deep)),
            out _, out _, out _), Is.EqualTo(MessageDecodeResult.InvalidPayload));
    }

    [Test]
    public void AtomicSaveKeepsPreviousValidFileAsBackup()
    {
        string folder = Path.Combine(Path.GetTempPath(), "CrystalMagicAtomicTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "slot.json");
        try
        {
            CrystalMagic.Core.AtomicSaveFile.Write(path, "first");
            CrystalMagic.Core.AtomicSaveFile.Write(path, "second");
            Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
            Assert.That(File.ReadAllText(Path.ChangeExtension(path, ".backup.json")), Is.EqualTo("first"));
            CrystalMagic.Core.AtomicSaveFile.Write(path, "third");
            Assert.That(File.ReadAllText(Path.ChangeExtension(path, ".backup.json")), Is.EqualTo("second"));
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    [Test]
    public void AtomicSaveFailureDoesNotTruncateExistingSave()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Ignore("This test verifies Windows exclusive-file replacement semantics.");
        string folder = Path.Combine(Path.GetTempPath(), "CrystalMagicAtomicTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "slot.json");
        try
        {
            CrystalMagic.Core.AtomicSaveFile.Write(path, "original");
            using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Catch<Exception>(() => CrystalMagic.Core.AtomicSaveFile.Write(path, "incomplete"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("original"));
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    [Test]
    public void MissingFrameAcknowledgementsCannotGrowTimingHistoryWithoutBound()
    {
        using Connect connect = new();
        for (int i = 0; i < 5000; i++) connect.RecordBattleFrameSend(i);
        var pending = (Dictionary<uint, long>)typeof(Connect).GetField("pendingBattleFrameSendTimes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(connect);
        Assert.That(pending.Count, Is.EqualTo(1024));
        connect.AcknowledgeBattleFrame(5000, 5010);
        Assert.That(pending.Count, Is.Zero);
        Assert.That(connect.RttMs, Is.EqualTo(11));
    }

    private sealed class TestEndpoint : NetworkEndpoint
    {
        public override string ToString() => "test-peer";
    }

    private sealed class RecordingTransport : IConnectionTransport
    {
        public NetworkEndpoint RemoteEndpoint { get; } = new TestEndpoint();
        public readonly List<byte[]> Messages = new();
        public void Send(byte[] message) => Messages.Add(message);
    }
}
