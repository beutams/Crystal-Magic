using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Server;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class NetworkInputAndProtocolTests
{
    [Test]
    public void EveryClickIsQueuedAndRecordedInTheSameFrameInOrder()
    {
        ClientFrameManager client = new() { running = true, connect = new Connect(), currentFrame = 98 };
        NetworkPlayerOperationData[] operations =
        {
            new NetworkPrimaryPressData { pointerX = 1 },
            new NetworkSkillChainSelectData { skillChainIndex = 3 },
            new NetworkPrimaryPressData { pointerX = 2 },
            new NetworkInteractData(),
            new NetworkInteractData(),
            new NetworkPropUseData { slotIndex = 1 },
            new NetworkPropUseData { slotIndex = 1 },
        };
        foreach (NetworkPlayerOperationData operation in operations)
            Assert.That(client.EnqueueOperation(operation), Is.True);

        Guid unitId = Guid.NewGuid();
        Queue<NetworkStateData> states = new();
        client.AppendPendingOperations(unitId, states);
        CollectionAssert.AreEqual(operations, states);
        CollectionAssert.AreEqual(operations, client.inputOrder[98]);
        foreach (NetworkStateData state in states)
            Assert.That(state.unitId, Is.EqualTo(unitId));
        client.AppendPendingOperations(unitId, states);
        Assert.That(states.Count, Is.EqualTo(operations.Length));
    }

    [Test]
    public void SkillSelectionRejectsOlderFramesButAcceptsCorrectionForDroppedInput()
    {
        ClientFrameManager client = new() { running = true, connect = new Connect(), currentFrame = 98 };
        client.EnqueueOperation(new NetworkSkillChainSelectData { skillChainIndex = 3 });
        Assert.That(client.ShouldApplySkillChainState(97), Is.False);
        client.currentFrame = 99;
        client.EnqueueOperation(new NetworkSkillChainSelectData { skillChainIndex = 4 });
        Assert.That(client.ShouldApplySkillChainState(98), Is.False);
        // 无需收到某个操作序号；晚到丢弃后，后续服务器帧也可以纠正。
        Assert.That(client.ShouldApplySkillChainState(100), Is.True);
        Assert.That(client.ShouldApplySkillChainState(99), Is.False);
    }

    [Test]
    public void ServerDropsLateFramesAndProcessesEveryEventOnlyInItsOwnFrame()
    {
        TCPPacketCode.Init();
        Guid unitId = Guid.NewGuid();
        Connect connect = new();
        ServerFrameManager server = new() { currentFrame = 10, sceneVersion = 4 };
        server.AddConnect(connect, unitId);

        server.OnReceiveMessage(CreateFrame(4, 9, unitId, new NetworkInteractData()), connect);
        NetworkPlayerOperationData[] current = { new NetworkInteractData(), new NetworkInteractData() };
        General_FrameStateData currentMessage = CreateFrame(4, 10, unitId, current);
        server.OnReceiveMessage(currentMessage, connect);
        server.OnReceiveMessage(currentMessage, connect);
        NetworkPrimaryPressData future = new();
        server.OnReceiveMessage(CreateFrame(4, 12, unitId, future), connect);

        List<NetworkStateData> applied = new();
        server.onHandleReceive = (frame, states) =>
        {
            Assert.That(frame, Is.EqualTo(server.currentFrame));
            while (states.Count > 0)
                applied.Add(states.Dequeue().data);
        };
        server.HandleReceive();
        CollectionAssert.AreEqual(current, applied);
        server.currentFrame = 11;
        server.HandleReceive();
        Assert.That(applied.Count, Is.EqualTo(2));
        server.currentFrame = 12;
        server.HandleReceive();
        server.HandleReceive();
        Assert.That(applied.Count, Is.EqualTo(3));
        Assert.That(applied[2], Is.SameAs(future));
        Assert.That(server.receivedOrder, Is.Empty);
    }

    [Test]
    public void PacketReaderReportsIncompleteAndInvalidPacketsWithoutThrowing()
    {
        using MemoryStream partial = new();
        partial.WriteByte(0);
        partial.WriteByte(0);
        Assert.That(
            TCPPacketCode.TryUnPack(partial, out _, out _, out _),
            Is.EqualTo(PacketReadResult.NeedMoreData));

        using MemoryStream invalid = new();
        invalid.Write(new byte[] { 0x00, 0x10, 0x00, 0x01 }, 0, 4);
        Assert.That(
            TCPPacketCode.TryUnPack(invalid, out _, out _, out string error),
            Is.EqualTo(PacketReadResult.Invalid));
        Assert.That(error, Does.Contain("包长不合法"));
    }

    [Test]
    public void UnknownOpcodeIsReportedWithoutIndexingException()
    {
        TCPPacketCode.Init();
        MessageDecodeResult result = TCPPacketCode.TryToMessage(
            Array.Empty<byte>(),
            ushort.MaxValue,
            out IMessage message,
            out Exception exception);
        Assert.That(result, Is.EqualTo(MessageDecodeResult.UnknownOpcode));
        Assert.That(message, Is.Null);
        Assert.That(exception, Is.Null);
    }

    [Test]
    public void DisconnectIsDeduplicatedAndCallbackFailuresDoNotBlockCleanup()
    {
        TestService service = new();
        Connect connect = service.AddConnection();
        int serviceCallbacks = 0;
        int connectCallbacks = 0;
        LogAssert.Expect(LogType.Error, new Regex(@"\[TCP\]\[Callback\].*expected test failure"));
        service.OnDisconnected += _ => throw new InvalidOperationException("expected test failure");
        service.OnDisconnected += _ => serviceCallbacks++;
        connect.OnDisconnected += _ => connectCallbacks++;

        service.QueueDisconnect(DisconnectReason.ReceiveError);
        service.QueueDisconnect(DisconnectReason.Timeout);
        service.FlushDisconnects();

        Assert.That(serviceCallbacks, Is.EqualTo(1));
        Assert.That(connectCallbacks, Is.EqualTo(1));
        Assert.That(connect.State, Is.EqualTo(ConnectState.Close));
        Assert.That(connect.LastDisconnectInfo.Reason, Is.EqualTo(DisconnectReason.ReceiveError));
    }

    private static General_FrameStateData CreateFrame(
        uint sceneVersion,
        uint frameId,
        Guid unitId,
        params NetworkPlayerOperationData[] operations)
    {
        List<NetworkStateData> states = new();
        foreach (NetworkPlayerOperationData operation in operations)
        {
            operation.unitId = unitId;
            states.Add(operation);
        }

        return new General_FrameStateData
        {
            sceneVersion = sceneVersion,
            data = new NetworkFrameData
            {
                frameId = frameId,
                datas = states,
            },
        };
    }

    [TestCase(DisconnectReason.InvalidPacket)]
    [TestCase(DisconnectReason.UnknownOpcode)]
    [TestCase(DisconnectReason.DeserializeError)]
    [TestCase(DisconnectReason.HandlerError)]
    [TestCase(DisconnectReason.ReceiveError)]
    public void FaultyConnectionDoesNotPreventAnotherConnectionReceiving(DisconnectReason reason)
    {
        TCPPacketCode.Init();
        using TestService service = new();
        Connect bad = service.AddLoopbackConnection(out Socket badPeer, out Socket badSocket);
        using (badPeer)
        {
            Connect good = service.AddLoopbackConnection(out Socket goodPeer, out Socket goodSocket);
            using (goodPeer)
            {
                ushort opcode = TCPPacketCode.GetOpcode<C2S_Ping>();
                byte[] valid = TCPPacketCode.Pack(opcode, TCPPacketCode.ToJson(new C2S_Ping()));
                int received = 0;
                int disconnected = 0;
                good.RegisterCallback(opcode, (_, _) => received++);
                service.OnDisconnected += _ => disconnected++;
                if (reason == DisconnectReason.ReceiveError)
                    badSocket.Dispose();
                else
                {
                    byte[] invalid = reason switch
                    {
                        DisconnectReason.InvalidPacket => new byte[] { 0, 0, 0, 1 },
                        DisconnectReason.UnknownOpcode => TCPPacketCode.Pack(ushort.MaxValue, Array.Empty<byte>()),
                        DisconnectReason.DeserializeError => TCPPacketCode.Pack(opcode, new byte[] { (byte)'{' }),
                        _ => valid,
                    };
                    if (reason == DisconnectReason.HandlerError)
                        bad.RegisterCallback(opcode, (_, _) => throw new InvalidOperationException("bad handler"));
                    badPeer.Send(invalid);
                    Assert.That(badSocket.Poll(1000000, SelectMode.SelectRead), Is.True);
                }
                goodPeer.Send(valid);
                Assert.That(goodSocket.Poll(1000000, SelectMode.SelectRead), Is.True);

                Assert.DoesNotThrow(service.Receive);
                service.FlushDisconnects();
                service.FlushDisconnects();
                Assert.That(bad.LastDisconnectInfo.Reason, Is.EqualTo(reason));
                Assert.That(bad.State, Is.EqualTo(ConnectState.Close));
                Assert.That(disconnected, Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(1));
                Assert.That(good.State, Is.EqualTo(ConnectState.Connected));
            }
        }
    }

    [Test]
    public void SendCallbackCanQueueAnotherPacketWithoutLosingIt()
    {
        TCPPacketCode.Init();
        using TestService service = new();
        Connect connect = service.AddLoopbackConnection(out Socket peer, out _);
        using (peer)
        {
            connect.Send(new C2S_Ping { Time = 1 });
            service.OnSend += sent => sent.Send(new C2S_Ping { Time = 2 });
            service.Send();
            Assert.That(TCPPacketCode.TryUnPack(connect.sendSteam, out byte[] body, out ushort opcode, out _),
                Is.EqualTo(PacketReadResult.Success));
            Assert.That(TCPPacketCode.TryToMessage(body, opcode, out IMessage message, out _),
                Is.EqualTo(MessageDecodeResult.Success));
            Assert.That(((C2S_Ping)message).Time, Is.EqualTo(2));
        }
    }

    private sealed class TestService : Service, IDisposable
    {
        private Guid connectionId;

        public override void Init() { }
        public override void Update() { }

        public Connect AddLoopbackConnection(out Socket peer, out Socket accepted)
        {
            using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            peer.Connect(listener.LocalEndPoint);
            accepted = listener.Accept();
            TCPPair pair = TCPPair.CreateTCPPair(accepted, (IPEndPoint)accepted.RemoteEndPoint, out connectionId);
            pair.connect.State = ConnectState.Connected;
            connects.Add(connectionId, pair);
            return pair.connect;
        }

        public void Receive() => HandleRecv();
        public void Send() => HandleSend();

        public void Dispose()
        {
            foreach (TCPPair pair in connects.Values)
            {
                pair.socket.Dispose();
                pair.connect.readSteam.Dispose();
                pair.connect.sendSteam.Dispose();
                pair.connect.Dispose();
            }
            connects.Clear();
        }

        public Connect AddConnection()
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TCPPair pair = TCPPair.CreateTCPPair(
                socket,
                new IPEndPoint(IPAddress.Loopback, 1),
                out connectionId);
            pair.connect.State = ConnectState.Connected;
            connects.Add(connectionId, pair);
            return pair.connect;
        }

        public void QueueDisconnect(DisconnectReason reason)
        {
            MarkDisconnected(connectionId, reason, "Test");
        }

        public void FlushDisconnects()
        {
            HandleDisconnect();
        }
    }
}
