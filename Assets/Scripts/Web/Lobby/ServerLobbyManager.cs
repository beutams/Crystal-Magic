using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ServerLobbyManager : Singleton<ServerLobbyManager>
    {
        public ServerService lobbyService;
        public Dictionary<ulong, Room> roomList = new Dictionary<ulong, Room>();
        public Dictionary<ulong, Player> playerList = new Dictionary<ulong, Player>();
        public Dictionary<Connect, ulong> connectAccountDic = new Dictionary<Connect, ulong>();
        public ClientService clientService;
        public Connect battleConnect;

        protected override void Awake()
        {
            base.Awake();
            TCPPacketCode.Init();
            lobbyService = new ServerService(ServerUtility.GetLobbyIPEndPoint());
            lobbyService.OnAccept += OnAccept;
            lobbyService.OnDisconnected += OnDisconnected;

            lobbyService.Init();

            clientService = new ClientService();
            clientService.Init();
            clientService.Connect(ServerUtility.GetBattleLobbyIPEndPoint(), out battleConnect);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2L_StartRoomResult>(),OnBattleStart);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2L_ReloadRoomResult>(), OnBattleReload);
        }
        #region Server
        private void OnDisconnected(Connect connect)
        {
            if (!connectAccountDic.TryGetValue(connect, out ulong accountId))
            {
                connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);
                return;
            }

            Player player = playerList[accountId];
            if(player.roomId != 0UL)
            {
                Room room = roomList[player.roomId];
                room.players.Remove(accountId);
                room.enterNum = room.players.Count;
                if (room.players.Count != 0)
                {
                    if (room.ownerAccountId == accountId)
                    {
                        room.ownerAccountId = room.players.First().Key;
                    }

                    RoomData roomData = RoomData.CreateRoomData(room);
                    foreach (Player member in room.players.Values)
                    {
                        member.connect.Send(new L2C_RefreshRoomInfo
                        {
                            roomData = roomData
                        });
                    }
                }
                else
                {
                    roomList.Remove(player.roomId);
                }
            }
            connectAccountDic.Remove(connect);
            playerList.Remove(accountId);
            BroadcastRoomListChanged();

            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_CreateRoom>(), OnClientCreateRoom);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_JoinRoom>(), OnClientJoinRoom);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LeaveRoom>(), OnClientLeaveRoom);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_Ready>(), OnClientReady);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_SetDungeonFloor>(), OnClientSetDungeonFloor);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_Start>(), OnClientStart);
        }
        private void OnAccept(Connect connect)
        {
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);
        }
        private void OnClientLogin(IMessage message, Connect connect)
        {
            C2L_LoginLobby loginMessage = message as C2L_LoginLobby;
            if (loginMessage != null)
            {
                CreatePlayer(connect, loginMessage.accountId, loginMessage.username);
            }
        }
        private bool CreatePlayer(Connect connect, ulong accountId, string username)
        {
            if (accountId == 0UL
                || connectAccountDic.ContainsKey(connect)
                || playerList.ContainsKey(accountId))
            {
                return false;
            }

            Player player = new Player
            {
                accountId = accountId,
                username = username,
                roomId = 0UL,
                connect = connect,
            };
            connectAccountDic.Add(connect, accountId);
            playerList.Add(accountId, player);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);

            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_CreateRoom>(), OnClientCreateRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_JoinRoom>(), OnClientJoinRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_LeaveRoom>(), OnClientLeaveRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_Ready>(), OnClientReady);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_SetDungeonFloor>(), OnClientSetDungeonFloor);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_Start>(), OnClientStart);

            connect.Send(new L2C_RefreshRoomList
            {
                roomListData = RoomListData.CreateRoomData(roomList)
            });
            battleConnect.Send(new L2B_TryReloadRoom
            {
                accountId = accountId,
            });
            return true;
        }
        private void OnClientStart(IMessage message, Connect connect)
        {
            if(connectAccountDic.TryGetValue(connect, out var accountId) && playerList.TryGetValue(accountId, out Player player) && roomList.TryGetValue(player.roomId,out var room))
            {
                if (room.ownerAccountId != accountId)
                    return;
                bool allReady = true;
                foreach (var p in room.players)
                {
                    if(!p.Value.ready)
                    {
                        allReady = false;
                        break;
                    }
                }
                if (!allReady)
                    return;

                battleConnect.Send(new L2B_StartRoom()
                {
                    roomId = room.roomId,
                    ownerAccountId = room.ownerAccountId,
                    dungeonFloor = room.dungeonFloor,
                    players = room.players.Keys.ToArray()
                });
            }
        }
        private void OnClientReady(IMessage message, Connect connect)
        {
            C2L_Ready realMessage = message as C2L_Ready;
            if (connectAccountDic.TryGetValue(connect, out ulong accountId) && playerList.TryGetValue(accountId, out Player player) && roomList.TryGetValue(player.roomId,out Room room))
            {
                player.ready = realMessage.ready;

                RoomData roomData = RoomData.CreateRoomData(room);
                foreach (var p in room.players)
                {
                    p.Value.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
                }
            }
        }
        private void OnClientSetDungeonFloor(IMessage message, Connect connect)
        {
            C2L_SetDungeonFloor realMessage = message as C2L_SetDungeonFloor;
            if (realMessage != null
                && connectAccountDic.TryGetValue(connect, out ulong accountId)
                && playerList.TryGetValue(accountId, out Player player)
                && roomList.TryGetValue(player.roomId, out Room room)
                && room.ownerAccountId == accountId)
            {
                room.dungeonFloor = Math.Max(1, realMessage.dungeonFloor);

                RoomData roomData = RoomData.CreateRoomData(room);
                foreach (Player member in room.players.Values)
                {
                    member.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
                }
            }
        }
        private void OnClientCreateRoom(IMessage message, Connect connect)
        {
            C2L_CreateRoom realMessage = message as C2L_CreateRoom;
            if (connectAccountDic.TryGetValue(connect, out ulong accountId) && playerList.TryGetValue(accountId, out Player player) && player.roomId == 0UL)
            {
                Room room = new Room();
                room.roomId = ServerUtility.CreateRoomId();
                room.ownerAccountId = accountId;
                room.roomName = realMessage.roomName;
                room.enterNum = 1;
                room.maxNum = 4;
                room.dungeonFloor = 1;
                room.start = false;
                room.players.Add(player.accountId, player);

                player.roomId = room.roomId;

                RoomData roomData = RoomData.CreateRoomData(room);

                roomList.Add(room.roomId, room);

                connect.Send(new L2C_CreateReturn() { type = LobbyRequestType.CreateSuccess, roomData = roomData });
                BroadcastRoomListChanged();
            }
            else
            {
                connect.Send(new L2C_CreateReturn() { type = LobbyRequestType.CreateFail });
            }
        }
        private void OnClientJoinRoom(IMessage message, Connect connect)
        {
            C2L_JoinRoom data = message as C2L_JoinRoom;
            if (!connectAccountDic.TryGetValue(connect, out ulong accountId)
                || !playerList.TryGetValue(accountId, out Player player)
                || player.roomId != 0UL)
            {
                connect.Send(new L2C_JoinReturn() { type = LobbyRequestType.JoinFail });
                return;
            }
            if(!roomList.TryGetValue(data.roomId, out Room room))
            {
                connect.Send(new L2C_JoinReturn() { type = LobbyRequestType.JoinRoomClosed });
                return;
            }
            if (room.players.Count >= room.maxNum)
            {
                connect.Send(new L2C_JoinReturn() { type = LobbyRequestType.JoinRoomFull });
                return;
            }

            player.roomId = room.roomId;

            room.players.Add(accountId, player);
            room.enterNum = room.players.Count;

            RoomData roomData = RoomData.CreateRoomData(room);

            connect.Send(new L2C_JoinReturn() { type = LobbyRequestType.JoinSuccess, roomData = roomData });
            foreach (var elsePlayer in room.players)
            {
                if (elsePlayer.Key == accountId)
                    continue;

                elsePlayer.Value.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
            }
            BroadcastRoomListChanged();
        }
        private void OnClientLeaveRoom(IMessage message, Connect connect)
        {
            if (!connectAccountDic.TryGetValue(connect, out ulong accountId)
                || !playerList.TryGetValue(accountId, out Player player)
                || player.roomId == 0UL
                || !roomList.TryGetValue(player.roomId, out Room room))
            {
                connect.Send(new L2C_LeaveReturn() { type = LobbyRequestType.LeaveFail });
                return;
            }
            player.roomId = 0UL;
            room.players.Remove(accountId);
            room.enterNum = room.players.Count;

            connect.Send(new L2C_LeaveReturn() { type = LobbyRequestType.LeaveSuccess });

            if(room.players.Count == 0)
            {
                roomList.Remove(room.roomId);
            }
            else
            {
                if (room.ownerAccountId == accountId)
                {
                    room.ownerAccountId = room.players.First().Key;
                }

                RoomData roomData = RoomData.CreateRoomData(room);
                foreach (var elsePlayer in room.players)
                {
                    if (elsePlayer.Key == accountId)
                        continue;
                    elsePlayer.Value.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
                }
            }
            BroadcastRoomListChanged();
        }
        private void BroadcastRoomListChanged()
        {
            foreach (var player in playerList.Values)
            {
                if (player.start)
                    continue;
                player.connect.Send(new L2C_RefreshRoomList() { roomListData = RoomListData.CreateRoomData(roomList) });
            }
        }
        private void LateUpdate()
        {
            lobbyService.Update();
            clientService.Update();
        }
        #endregion
        #region Client
        public void OnBattleStart(IMessage message, Connect connect)
        {
            B2L_StartRoomResult realMessage = message as B2L_StartRoomResult;
            if (roomList.TryGetValue(realMessage.roomId,out Room room))
            {
                foreach(var item in realMessage.secretKeys)
                {
                    if(playerList.TryGetValue(item.Key,out Player player))
                    {
                        player.connect.Send(new L2C_StartTicket() { ticket = item.Value });
                        playerList.Remove(item.Key);
                        connectAccountDic.Remove(player.connect);
                    }
                }
                roomList.Remove(realMessage.roomId);
            }
        }
        public void OnBattleReload(IMessage message, Connect connect)
        {
            B2L_ReloadRoomResult realMessage = message as B2L_ReloadRoomResult;
            if (realMessage == null ||
                string.IsNullOrEmpty(realMessage.ticket) ||
                !playerList.TryGetValue(realMessage.accountId, out Player player))
            {
                return;
            }

            player.connect.Send(new L2C_StartTicket { ticket = realMessage.ticket });
            playerList.Remove(realMessage.accountId);
            connectAccountDic.Remove(player.connect);
        }
        #endregion
    }
}
