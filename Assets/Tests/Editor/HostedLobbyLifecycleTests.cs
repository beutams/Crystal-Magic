using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Server;

public sealed class HostedLobbyLifecycleTests
{
    [Test]
    public void SteamLobbyLogsInAndCreatesHostWithoutDedicatedBattleProcess()
    {
        using Fixture f = new();
        f.StartRoom();
        Assert.That(f.Start.connection.kind, Is.EqualTo(BattleTransportKind.Steam));
        Assert.That(f.Start.connection.hostSteamId, Is.EqualTo(101UL));
        Assert.That(f.Start.room.players, Is.EquivalentTo(new ulong[] { 101, 202 }));
        Assert.That(f.Manager.battleConnect, Is.Null);
        Assert.That(f.Tickets, Is.Zero, "No ticket before the listener is ready.");
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        Assert.That(f.Manager.roomList, Is.Empty);
        Assert.That(f.Host.State, Is.EqualTo(ConnectState.Connected));
        Assert.That(f.Guest.State, Is.EqualTo(ConnectState.Connected));
    }

    [Test]
    public void HostDisconnectDuringStartResetsRoomAndDoesNotStrandRemainingPlayer()
    {
        using Fixture f = new();
        f.StartRoom();
        f.Client.Disconnect(f.Host);
        f.Pump(() => f.Manager.roomList[f.RoomId].ownerAccountId == 202);
        Room room = f.Manager.roomList[f.RoomId];
        Assert.That(room.start, Is.False);
        Assert.That(room.players[202].ready, Is.False);
        Assert.That(f.Tickets, Is.Zero);
    }

    [Test]
    public void HostReadyFromWrongPlayerAndOldSessionAreIgnored()
    {
        using Fixture f = new();
        f.StartRoom();
        f.Guest.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        B2L_StartRoomResult old = f.ReadyResult();
        old.connection = new BattleConnectionInfo { sessionId = Guid.NewGuid().ToString("N"), kind = BattleTransportKind.Steam, hostSteamId = 101 };
        f.Host.Send(new C2L_HostBattleReady { result = old });
        f.Tick(20);
        Assert.That(f.Tickets, Is.Zero);
        Assert.That(f.Manager.roomList[f.RoomId].start, Is.True);
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Tick(20);
        Assert.That(f.Tickets, Is.EqualTo(2));
    }

    [Test]
    public void HostFailureAndTimeoutReleaseStartStateAndRejectLateReady()
    {
        using Fixture f = new();
        f.StartRoom();
        B2L_StartRoomResult ready = f.ReadyResult();
        ready.error = "Native Steam listen failed";
        f.Host.Send(new C2L_HostBattleReady { result = ready });
        f.Pump(() => !f.Manager.roomList[f.RoomId].start);
        Assert.That(f.Manager.roomList[f.RoomId].players[202].ready, Is.False);
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Tick(20);
        Assert.That(f.Tickets, Is.Zero);
        f.StartRoom();
        System.Collections.IDictionary sessions = (System.Collections.IDictionary)typeof(ServerLobbyManager)
            .GetField("hostedSessions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f.Manager);
        foreach (object session in sessions.Values)
            session.GetType().GetField("Deadline").SetValue(session, -1L);
        f.Pump(() => !f.Manager.roomList[f.RoomId].start);
        Assert.That(sessions.Count, Is.Zero);
    }

    [Test]
    public void HostControlDisconnectAfterStartDoesNotAbortHealthyGuestBattleControl()
    {
        using Fixture f = new();
        f.StartRoom();
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        f.Client.Disconnect(f.Host);
        f.Pump(() => !f.Manager.playerList.ContainsKey(101));
        Assert.That(f.Guest.State, Is.EqualTo(ConnectState.Connected));
        Assert.That(f.Manager.playerList[202].start, Is.True);
        Assert.That(f.Cancels, Is.Zero);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly ServerLobbyManager Manager = new();
        public readonly ClientService Client = new();
        public Connect Host;
        public Connect Guest;
        public ulong RoomId;
        public int Tickets;
        public int Cancels;
        public L2C_HostBattleStart Start;
        public Fixture()
        {
            MessageCodec.Init();
            ServerService server = new(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
            Manager.Initialize(server, null, true);
            Client.Init();
            Client.Connect(server.LocalEndpoint, out Host);
            Client.Connect(server.LocalEndpoint, out Guest);
            foreach (Connect connection in new[] { Host, Guest })
            {
                ulong id = connection == Host ? 101UL : 202UL;
                connection.OnConnected += peer => peer.Send(new C2L_LoginLobby
                {
                    accountId = id, saveGuid = Guid.NewGuid().ToString("N"), username = "test",
                    steamP2PAvailable = true, protocolVersion = BattleConnectionInfo.CurrentProtocolVersion,
                });
                connection.RegisterCallback(MessageCodec.GetOpcode<L2C_StartTicket>(), (_, _) => Tickets++);
                connection.RegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleCancel>(), (_, _) => Cancels++);
            }
            Host.RegisterCallback(MessageCodec.GetOpcode<L2C_CreateReturn>(), (message, _) => RoomId = ((L2C_CreateReturn)message).roomData.roomId);
            Host.RegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleStart>(), (message, _) => Start = (L2C_HostBattleStart)message);
            Pump(() => Manager.playerList.Count == 2);
            Host.Send(new C2L_CreateRoom { roomName = "test" });
            Pump(() => RoomId != 0);
            Guest.Send(new C2L_JoinRoom { roomId = RoomId });
            Pump(() => Manager.roomList[RoomId].players.Count == 2);
        }
        public void StartRoom()
        {
            Start = null;
            Guest.Send(new C2L_Ready { ready = true });
            Pump(() => Manager.roomList[RoomId].players[202].ready);
            Host.Send(new C2L_Start());
            Pump(() => Start != null);
        }
        public B2L_StartRoomResult ReadyResult() => new()
        {
            roomId = RoomId, connection = Start.connection,
            secretKeys = new Dictionary<ulong, string> { [101] = "host-ticket", [202] = "guest-ticket" },
        };
        public void Tick(int frames)
        {
            for (int i = 0; i < frames; i++) { Manager.Update(); Client.Update(); Thread.Sleep(1); }
        }
        public void Pump(Func<bool> done)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (!done() && timer.ElapsedMilliseconds < 3000) Tick(1);
            Assert.That(done(), Is.True, "Hosted lobby flow timed out.");
        }
        public void Dispose() { Client.Shutdown(); Manager.Cleanup(); }
    }
}
