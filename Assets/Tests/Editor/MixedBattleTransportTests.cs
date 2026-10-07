using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using CrystalMagic.Core;
using NUnit.Framework;
using Server;

public sealed class MixedBattleTransportTests
{
    private static BattleConnectionInfo MixedConnection() => new()
    {
        sessionId = Guid.NewGuid().ToString("N"), hostAccountId = 101, hostSteamId = 101,
        kind = BattleTransportKind.Steam, address = "127.0.0.1", port = 13000, steamPort = 7,
    };

    [Test]
    public void RouteSelectionPreservesSessionAndKeepsTcpAndSteamPortsSeparate()
    {
        var both = MixedConnection();
        var socket = both.ForTransport(false);
        var steam = both.ForTransport(true);
        Assert.That(((TcpEndpoint)socket.ToEndpoint()).Address.Port, Is.EqualTo(13000));
        Assert.That(((SteamEndpoint)steam.ToEndpoint()).VirtualPort, Is.EqualTo(7));
        Assert.That(socket.sessionId, Is.EqualTo(steam.sessionId));
        Assert.That(socket.hostAccountId, Is.EqualTo(steam.hostAccountId));
        Assert.That(both.kind, Is.EqualTo(BattleTransportKind.Steam), "Per-player routes must not mutate the session descriptor.");
        socket.port = 9999;
        Assert.That(both.port, Is.EqualTo(13000));
    }

    [Test]
    public void HostRequestRequiresMatchingIdentityAndACompleteTransportRoster()
    {
        ulong local = ClientAccountIdentity.LocalNamespace | 1;
        var connection = MixedConnection();
        var request = new L2C_HostBattleStart
        {
            connection = connection, tcpRequired = true, steamMembers = new ulong[] { 101 },
            room = new L2B_StartRoom { sessionId = connection.sessionId, ownerAccountId = local, players = new[] { local, 101UL } },
        };
        Assert.That(HostedBattlePolicy.IsHostRequest(request, 101, 101), Is.True);
        Assert.That(HostedBattlePolicy.IsHostRequest(request, local, 0), Is.False);
        Assert.That(HostedBattlePolicy.IsHostRequest(request, 101, 0), Is.False);
        request.tcpRequired = false;
        Assert.That(HostedBattlePolicy.IsHostRequest(request, 101, 101), Is.False);
        request.tcpRequired = true; request.steamMembers = Array.Empty<ulong>();
        Assert.That(HostedBattlePolicy.IsHostRequest(request, 101, 101), Is.False);
        request.room.players = new[] { local };
        connection.hostAccountId = local; connection.hostSteamId = 0; connection.kind = BattleTransportKind.Tcp; connection.port = 0;
        Assert.That(HostedBattlePolicy.IsHostRequest(request, local, 0), Is.True, "A pending TCP listener may request an automatically allocated port.");
    }

    [Test]
    public void TcpCannotImpersonateSteamMemberEvenWithItsTicket()
    {
        var connection = MixedConnection();
        var tcp = new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 1234));
        ulong local = ClientAccountIdentity.LocalNamespace | 1;
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, local, tcp), Is.True);
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, 202, tcp), Is.False);
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, 202, new SteamEndpoint(202, 0)), Is.True);
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, 202, new SteamEndpoint(101, 0)), Is.False);
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, local, new SteamEndpoint(local, 0)), Is.False);
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, local, new LoopbackEndpoint(local)), Is.True);
        Assert.That(HostedBattlePolicy.MatchesPeer(connection, 202, new LoopbackEndpoint(101)), Is.False);
        Assert.That(HostedBattlePolicy.MatchesPeer(BattleConnectionInfo.Dedicated(connection.sessionId), 202, tcp), Is.True);
    }

    [Test]
    public void ReadyRejectsAddressSessionPortAndIdentityChanges()
    {
        var expected = MixedConnection();
        expected.port = 0;
        var actual = expected.ForTransport(true);
        actual.port = 13000;
        Assert.That(HostedBattlePolicy.IsReadyDescriptor(expected, actual, true), Is.True);
        actual.address = "0.0.0.0";
        Assert.That(HostedBattlePolicy.IsReadyDescriptor(expected, actual, true), Is.False);
        actual.address = expected.address;
        actual.hostSteamId++;
        Assert.That(HostedBattlePolicy.IsReadyDescriptor(expected, actual, true), Is.False);
        actual.hostSteamId = expected.hostSteamId;
        expected.port = 13001;
        Assert.That(HostedBattlePolicy.IsReadyDescriptor(expected, actual, true), Is.False);
        actual.protocolVersion--;
        Assert.That(actual.IsValid, Is.False);
    }

    [Test]
    public void CompositeReceivesAndBroadcastsTheSameMessagesAcrossTcpAndLocalQueue()
    {
        MessageCodec.Init();
        LoopbackTransport.CreatePair(101, out var localServer, out var localClient);
        var tcpServer = new ServerService(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
        var composite = new CompositeServerTransport(localServer, tcpServer);
        var tcpClient = new ClientService();
        var peers = new List<Connect>();
        var received = new List<long>();
        int localReplies = 0, tcpReplies = 0, disconnected = 0;
        composite.OnAccept += peer =>
        {
            peers.Add(peer);
            peer.RegisterCallback(MessageCodec.GetOpcode<C2S_Ping>(), (message, _) => received.Add(((C2S_Ping)message).Time));
        };
        composite.OnDisconnected += _ => disconnected++;
        void Pump(Func<bool> done)
        {
            var timer = Stopwatch.StartNew();
            while (!done() && timer.ElapsedMilliseconds < 3000)
            { composite.Update(); localClient.Update(); tcpClient.Update(); Thread.Sleep(1); }
            Assert.That(done(), Is.True);
        }
        try
        {
            composite.Init(); localClient.Init(); tcpClient.Init();
            localClient.Connect(localServer.LocalEndpoint, out Connect local);
            tcpClient.Connect(tcpServer.LocalEndpoint, out Connect remote);
            local.RegisterCallback(MessageCodec.GetOpcode<S2C_Pong>(), (_, _) => localReplies++);
            remote.RegisterCallback(MessageCodec.GetOpcode<S2C_Pong>(), (_, _) => tcpReplies++);
            Pump(() => peers.Count == 2 && local.State == ConnectState.Connected && remote.State == ConnectState.Connected);
            local.Send(new C2S_Ping { Time = 11 }); remote.Send(new C2S_Ping { Time = 22 });
            Pump(() => received.Count == 2);
            Assert.That(received, Is.EquivalentTo(new long[] { 11, 22 }));
            // Both transports have built-in ping responses; compare after they drain.
            Pump(() => tcpReplies == 1 && localReplies == 1);
            int oldTcp = tcpReplies;
            foreach (Connect peer in peers) peer.Send(new S2C_Pong { Time = 42 });
            Pump(() => localReplies == 2 && tcpReplies == oldTcp + 1);
            Connect acceptedTcp = peers.Find(peer => peer.RemoteEndpoint is TcpEndpoint);
            composite.DisconnectAfterSend(acceptedTcp);
            Pump(() => remote.State == ConnectState.Close && disconnected == 1);
            Assert.That(local.State, Is.EqualTo(ConnectState.Connected));
            local.Send(new C2S_Ping { Time = 33 });
            Pump(() => received.Count == 3);
        }
        finally { tcpClient.Shutdown(); localClient.Shutdown(); composite.Shutdown(); }
    }

    [Test]
    public void CompositeDoesNotReportReadyWhenAChildReportsListenFailureWithoutThrowing()
    {
        LoopbackTransport.CreatePair(1, out var server, out var client);
        var failed = new FailedListener();
        var composite = new CompositeServerTransport(server, failed);
        int ready = 0, errors = 0;
        composite.OnListening += () => ready++;
        composite.OnListeningFail += () => errors++;
        Assert.Throws<InvalidOperationException>(composite.Init);
        Assert.That(ready, Is.Zero);
        Assert.That(errors, Is.EqualTo(1));
        Assert.That(failed.Closed, Is.True);
        client.Init();
        client.Connect(server.LocalEndpoint, out Connect pending);
        client.Update();
        Assert.That(pending.State, Is.EqualTo(ConnectState.Close), "Previously opened listeners must also be shut down.");
        client.Shutdown(); composite.Shutdown();
    }

    private sealed class FailedListener : IServerTransport
    {
        public bool Closed;
        public NetworkEndpoint LocalEndpoint => null;
        public event Action OnListening { add { } remove { } }
        public event Action OnListeningFail;
        public event Action<Connect> OnAccept { add { } remove { } }
        public event Action<Connect> OnSend { add { } remove { } }
        public event Action<Connect> OnRecv { add { } remove { } }
        public event Action<Connect> OnDisconnected { add { } remove { } }
        public void Init() => OnListeningFail?.Invoke();
        public void Update() { }
        public void Shutdown() => Closed = true;
        public void Disconnect(Connect connect) { }
        public void DisconnectAfterSend(Connect connect) { }
    }
}
