using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Server;

public sealed class RoomCreationNetworkTests
{
    [Test] public void CustomRoomSettingsRoundTripAndCapacityIncludesHost()
    {
        using Fixture f = new();
        f.Host.Send(new C2L_CreateRoom { roomName = "  双人地牢  ", displayName = "  自选昵称  ", maxPlayers = 2 });
        f.Pump(() => f.Created != null);
        Assert.That(f.Created.type, Is.EqualTo(LobbyRequestType.CreateSuccess));
        RoomData room = f.Created.roomData;
        Assert.That(room.roomName, Is.EqualTo("双人地牢"));
        Assert.That(room.players[101], Is.EqualTo("自选昵称"));
        Assert.That(room.maxNum, Is.EqualTo(2));
        f.Guest.Send(new C2L_JoinRoom { roomId = room.roomId }); f.Pump(() => f.GuestJoined != null);
        Assert.That(f.GuestJoined.type, Is.EqualTo(LobbyRequestType.JoinSuccess));
        f.Third.Send(new C2L_JoinRoom { roomId = room.roomId }); f.Pump(() => f.ThirdJoined != null);
        Assert.That(f.ThirdJoined.type, Is.EqualTo(LobbyRequestType.JoinRoomFull));
        Assert.That(f.Manager.playerList[303].roomId, Is.Zero);
        f.List = null; f.Third.Send(new C2L_RefreshRoomList()); f.Pump(() => f.List != null);
        SimpleRoomData listed = f.List.roomList[0];
        Assert.That(listed.ownerName, Is.EqualTo("自选昵称"));
        Assert.That(listed.roomName, Is.EqualTo("双人地牢"));
        Assert.That(listed.enterNum, Is.EqualTo(2)); Assert.That(listed.maxNum, Is.EqualTo(2));
    }

    [TestCase(1)] [TestCase(5)]
    public void InvalidCapacityDoesNotCreateRoomOrRenamePlayer(int capacity)
    {
        using Fixture f = new();
        f.Host.Send(new C2L_CreateRoom { roomName = "房间", displayName = "不应生效", maxPlayers = capacity });
        f.Pump(() => f.Created != null);
        Assert.That(f.Created.type, Is.EqualTo(LobbyRequestType.CreateFail));
        Assert.That(f.Manager.roomList, Is.Empty); Assert.That(f.Manager.playerList[101].username, Is.EqualTo("account101"));
        Assert.That(f.Manager.playerList[101].roomId, Is.Zero);
        f.Created = null;
        f.Host.Send(new C2L_CreateRoom { roomName = "重试", displayName = "有效昵称", maxPlayers = 3 });
        f.Pump(() => f.Created != null);
        Assert.That(f.Created.type, Is.EqualTo(LobbyRequestType.CreateSuccess));
        Assert.That(f.Created.roomData.maxNum, Is.EqualTo(3));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly ServerLobbyManager Manager = new();
        private readonly ClientService client = new();
        public Connect Host, Guest, Third;
        public L2C_CreateReturn Created;
        public L2C_JoinReturn GuestJoined, ThirdJoined;
        public RoomListData List;
        public Fixture()
        {
            MessageCodec.Init();
            var server = new ServerService(new TcpEndpoint(new IPEndPoint(IPAddress.Loopback, 0)));
            Manager.Initialize(server, null, true); client.Init();
            Connect Login(ulong id)
            {
                client.Connect(server.LocalEndpoint, out Connect connection);
                connection.OnConnected += peer => peer.Send(new C2L_LoginLobby { accountId = id,
                    saveGuid = Guid.NewGuid().ToString("N"), username = "account" + id,
                    protocolVersion = BattleConnectionInfo.CurrentProtocolVersion });
                return connection;
            }
            Host = Login(101); Guest = Login(202); Third = Login(303);
            Host.RegisterCallback(MessageCodec.GetOpcode<L2C_CreateReturn>(), (message, _) => Created = (L2C_CreateReturn)message);
            Guest.RegisterCallback(MessageCodec.GetOpcode<L2C_JoinReturn>(), (message, _) => GuestJoined = (L2C_JoinReturn)message);
            Third.RegisterCallback(MessageCodec.GetOpcode<L2C_JoinReturn>(), (message, _) => ThirdJoined = (L2C_JoinReturn)message);
            Third.RegisterCallback(MessageCodec.GetOpcode<L2C_RefreshRoomList>(), (message, _) => List = ((L2C_RefreshRoomList)message).roomListData);
            Pump(() => Manager.playerList.Count == 3);
        }
        public void Pump(Func<bool> done)
        {
            var timer = Stopwatch.StartNew();
            while (!done() && timer.ElapsedMilliseconds < 5000) { Manager.Update(); client.Update(); Thread.Sleep(1); }
            Assert.That(done(), Is.True, "Local lobby request timed out.");
        }
        public void Dispose() { client.Shutdown(); Manager.Cleanup(); }
    }
}
