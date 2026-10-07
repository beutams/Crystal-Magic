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

    [Test]
    public void MixedRoomIssuesDifferentTransportsForTheSameSession()
    {
        using Fixture f = new(localGuest: true);
        f.StartRoom();
        Assert.That(f.Start.tcpRequired, Is.True);
        Assert.That(f.Start.steamMembers, Is.EquivalentTo(new[] { f.HostId }));
        Assert.That(f.Start.connection.address, Is.EqualTo("127.0.0.1"));
        f.BattleHost.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        Assert.That(f.Routes[f.HostId].kind, Is.EqualTo(BattleTransportKind.Steam));
        Assert.That(f.Routes[f.GuestId].kind, Is.EqualTo(BattleTransportKind.Tcp));
        Assert.That(f.Routes[f.GuestId].port, Is.EqualTo(40123));
        Assert.That(f.Routes[f.GuestId].sessionId, Is.EqualTo(f.Routes[f.HostId].sessionId));
        Assert.That(f.Routes[f.GuestId].IsHosted, Is.True);
        Assert.That(f.Manager.playerList[f.GuestId].start, Is.True);
    }

    [Test]
    public void LocalOwnerKeepsOwnershipWhileSteamGuestHostsTheMixedBattle()
    {
        using Fixture f = new(localOwner: true);
        f.StartRoom();
        Assert.That(f.BattleHost, Is.SameAs(f.Guest));
        Assert.That(f.Start.room.ownerAccountId, Is.EqualTo(f.HostId));
        Assert.That(f.Start.connection.hostAccountId, Is.EqualTo(f.GuestId));
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Tick(15);
        Assert.That(f.Tickets, Is.Zero, "Only the actual battle host may confirm listening.");
        f.Guest.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        Assert.That(f.Routes[f.HostId].kind, Is.EqualTo(BattleTransportKind.Tcp));
        Assert.That(f.Routes[f.GuestId].kind, Is.EqualTo(BattleTransportKind.Steam));
    }

    [Test]
    public void AllLocalRoomNeedsNeitherSteamNorDedicatedBattleServer()
    {
        using Fixture f = new(localOwner: true, localGuest: true);
        f.StartRoom();
        Assert.That(f.Start.steamMembers, Is.Empty);
        Assert.That(f.Start.connection.hostSteamId, Is.Zero);
        Assert.That(f.Start.connection.hostAccountId, Is.EqualTo(f.HostId));
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        foreach (BattleConnectionInfo route in f.Routes.Values)
        { Assert.That(route.kind, Is.EqualTo(BattleTransportKind.Tcp)); Assert.That(route.IsValid, Is.True); }
        Assert.That(f.Manager.battleConnect, Is.Null);
    }

    [Test]
    public void MissingTcpListenerOrChangedHostIdentityRejectsReady()
    {
        foreach (bool missingPort in new[] { true, false })
        {
            using Fixture f = new(localGuest: true);
            f.StartRoom();
            var ready = f.ReadyResult();
            if (missingPort) ready.connection.port = 0; else ready.connection.hostAccountId++;
            f.Host.Send(new C2L_HostBattleReady { result = ready });
            f.Pump(() => !f.Manager.roomList[f.RoomId].start);
            Assert.That(f.Tickets, Is.Zero);
        }
    }

    [Test]
    public void MixedHostDisconnectDuringStartResetsLocalOwnersRoom()
    {
        using Fixture f = new(localOwner: true);
        f.StartRoom();
        f.Client.Disconnect(f.Guest);
        f.Pump(() => f.Manager.roomList[f.RoomId].players.Count == 1);
        Assert.That(f.Manager.roomList[f.RoomId].ownerAccountId, Is.EqualTo(f.HostId));
        Assert.That(f.Manager.roomList[f.RoomId].start, Is.False);
        Assert.That(f.Tickets, Is.Zero);
    }

    [Test]
    public void LocalGuestReloadRetainsTcpRouteAndGoesToActualSteamHost()
    {
        using Fixture f = new(localGuest: true);
        f.StartRoom();
        f.Host.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
        f.Pump(() => f.Tickets == 2);
        L2C_HostReloadRequest reload = null;
        f.Host.RegisterCallback(MessageCodec.GetOpcode<L2C_HostReloadRequest>(), (message, _) => reload = (L2C_HostReloadRequest)message);
        f.Client.Disconnect(f.Guest);
        f.Pump(() => !f.Manager.playerList.ContainsKey(f.GuestId));
        f.Client.Connect(f.Manager.lobbyService.LocalEndpoint, out Connect returning);
        BattleConnectionInfo route = null;
        returning.RegisterCallback(MessageCodec.GetOpcode<L2C_StartTicket>(), (message, _) => route = ((L2C_StartTicket)message).connection);
        returning.OnConnected += peer => peer.Send(new C2L_LoginLobby
        { accountId = f.GuestId, saveGuid = f.Saves[f.GuestId], username = "returning", protocolVersion = BattleConnectionInfo.CurrentProtocolVersion });
        f.Pump(() => reload != null);
        f.Host.Send(new C2L_HostReloadResult
        {
            sessionId = reload.sessionId, requestId = reload.requestId,
            result = new B2L_ReloadRoomResult { accountId = f.GuestId, saveGuid = f.Saves[f.GuestId],
                type = BattleReloadRoomResultType.ReloadAvailable, ticket = "replacement-ticket" },
        });
        f.Pump(() => route != null);
        Assert.That(route.kind, Is.EqualTo(BattleTransportKind.Tcp));
        Assert.That(route.sessionId, Is.EqualTo(f.Start.connection.sessionId));
        Assert.That(route.port, Is.EqualTo(40123));
    }

    [Test]
    public void ReconnectedBattleHostRegainsControlForBothMixedAndAllLocalRooms()
    {
        foreach (bool localGuest in new[] { false, true })
        {
            using Fixture f = new(localOwner: true, localGuest: localGuest);
            f.StartRoom();
            f.BattleHost.Send(new C2L_HostBattleReady { result = f.ReadyResult() });
            f.Pump(() => f.Tickets == 2);
            ulong hostId = f.Start.connection.hostAccountId;
            f.Client.Disconnect(f.BattleHost);
            f.Pump(() => !f.Manager.playerList.ContainsKey(hostId));
            f.Client.Connect(f.Manager.lobbyService.LocalEndpoint, out Connect returning);
            returning.OnConnected += peer => peer.Send(new C2L_LoginLobby
            {
                accountId = hostId, saveGuid = f.Saves[hostId], username = "returning-host",
                steamP2PAvailable = !CrystalMagic.Core.ClientAccountIdentity.IsLocalNetworkId(hostId),
                protocolVersion = BattleConnectionInfo.CurrentProtocolVersion, activeSessionId = f.Start.connection.sessionId,
            });
            f.Pump(() => f.Manager.playerList.ContainsKey(hostId));
            returning.Send(new C2L_HostBattleEnded { sessionId = f.Start.connection.sessionId });
            f.Pump(() => !f.Manager.playerList[f.HostId].start);
            Assert.That(f.Manager.playerList[f.GuestId].start, Is.False);
        }
    }

    [Test]
    public void LobbyRejectsOldProtocolAndLocalIdentityClaimingSteamTransport()
    {
        using Fixture f = new();
        foreach (bool oldProtocol in new[] { true, false })
        {
            f.Client.Connect(f.Manager.lobbyService.LocalEndpoint, out Connect rejected);
            LobbyRequestType? result = null;
            rejected.RegisterCallback(MessageCodec.GetOpcode<L2C_LoginLobbyResult>(), (message, _) => result = ((L2C_LoginLobbyResult)message).type);
            rejected.OnConnected += peer => peer.Send(new C2L_LoginLobby
            {
                accountId = CrystalMagic.Core.ClientAccountIdentity.LocalNamespace | 3,
                saveGuid = Guid.NewGuid().ToString("N"), username = "invalid",
                steamP2PAvailable = !oldProtocol,
                protocolVersion = oldProtocol ? 1 : BattleConnectionInfo.CurrentProtocolVersion,
            });
            f.Pump(() => rejected.State == ConnectState.Close);
            Assert.That(result, Is.EqualTo(LobbyRequestType.LoginFail));
            Assert.That(f.Manager.playerList.Count, Is.EqualTo(2));
        }
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
        public Connect BattleHost;
        public readonly ulong HostId;
        public readonly ulong GuestId;
        public readonly Dictionary<ulong, BattleConnectionInfo> Routes = new();
        public readonly Dictionary<ulong, string> Saves = new();
        public Fixture(bool localOwner = false, bool localGuest = false)
        {
            HostId = 101UL | (localOwner ? CrystalMagic.Core.ClientAccountIdentity.LocalNamespace : 0);
            GuestId = 202UL | (localGuest ? CrystalMagic.Core.ClientAccountIdentity.LocalNamespace : 0);
            MessageCodec.Init();
            ServerService server = new(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
            Manager.Initialize(server, null, true);
            Client.Init();
            Client.Connect(server.LocalEndpoint, out Host);
            Client.Connect(server.LocalEndpoint, out Guest);
            foreach (Connect connection in new[] { Host, Guest })
            {
                ulong id = connection == Host ? HostId : GuestId;
                Saves[id] = Guid.NewGuid().ToString("N");
                connection.OnConnected += peer => peer.Send(new C2L_LoginLobby
                {
                    accountId = id, saveGuid = Saves[id], username = "test",
                    steamP2PAvailable = !CrystalMagic.Core.ClientAccountIdentity.IsLocalNetworkId(id), protocolVersion = BattleConnectionInfo.CurrentProtocolVersion,
                });
                connection.RegisterCallback(MessageCodec.GetOpcode<L2C_StartTicket>(), (message, _) =>
                { Tickets++; Routes[id] = ((L2C_StartTicket)message).connection; });
                connection.RegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleCancel>(), (_, _) => Cancels++);
                connection.RegisterCallback(MessageCodec.GetOpcode<L2C_HostBattleStart>(), (message, _) =>
                { Start = (L2C_HostBattleStart)message; BattleHost = connection; });
            }
            Host.RegisterCallback(MessageCodec.GetOpcode<L2C_CreateReturn>(), (message, _) => RoomId = ((L2C_CreateReturn)message).roomData.roomId);
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
            Pump(() => Manager.roomList[RoomId].players[GuestId].ready);
            Host.Send(new C2L_Start());
            Pump(() => Start != null);
        }
        public B2L_StartRoomResult ReadyResult() => new()
        {
            roomId = RoomId, connection = new BattleConnectionInfo
            {
                sessionId = Start.connection.sessionId, kind = Start.connection.kind,
                hostAccountId = Start.connection.hostAccountId, hostSteamId = Start.connection.hostSteamId,
                address = Start.connection.address, port = Start.tcpRequired ? 40123 : Start.connection.port,
                steamPort = Start.connection.steamPort,
            },
            secretKeys = new Dictionary<ulong, string> { [HostId] = "host-ticket", [GuestId] = "guest-ticket" },
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
