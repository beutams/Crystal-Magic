using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ServerLobbyManager
    {
        public ServerService lobbyService;
        public Dictionary<ulong, Room> roomList = new Dictionary<ulong, Room>();
        public Dictionary<ulong, Player> playerList = new Dictionary<ulong, Player>();
        public Dictionary<Connect, ulong> connectAccountDic = new Dictionary<Connect, ulong>();
        private readonly Dictionary<ulong, Player> pendingPlayerList = new Dictionary<ulong, Player>();
        private readonly Dictionary<Connect, ulong> pendingConnectAccountDic = new Dictionary<Connect, ulong>();
        public ClientService clientService;
        public Connect battleConnect;
        private long battleReconnectTimerId;

        public void Initialize()
        {
            TCPPacketCode.Init();
            lobbyService = new ServerService(ServerUtility.GetLobbyIPEndPoint());
            lobbyService.OnAccept += OnAccept;
            lobbyService.OnDisconnected += OnDisconnected;

            lobbyService.Init();

            clientService = new ClientService();
            clientService.Init();
            ConnectBattle();
        }

        private void ConnectBattle()
        {
            if (battleConnect != null)
            {
                return;
            }

            clientService.Connect(ServerUtility.GetBattleLobbyIPEndPoint(), out battleConnect);
            battleConnect.OnConnected += OnBattleConnected;
            battleConnect.OnDisconnected += OnBattleDisconnected;
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2L_StartRoomResult>(), OnBattleStart);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2L_ReloadRoomResult>(), OnBattleReload);
        }

        private void OnBattleConnected(Connect connect)
        {
            if (connect != battleConnect)
            {
                return;
            }

            if (battleReconnectTimerId != 0)
            {
                NetworkTimer.Instance.Remove(battleReconnectTimerId);
                battleReconnectTimerId = 0;
            }

            foreach (Player player in playerList.Values)
            {
                battleConnect.Send(new L2B_TryReloadRoom
                {
                    accountId = player.accountId,
                    saveGuid = player.saveGuid,
                });
            }

            foreach (Player player in pendingPlayerList.Values)
            {
                battleConnect.Send(new L2B_TryReloadRoom
                {
                    accountId = player.accountId,
                    saveGuid = player.saveGuid,
                });
            }
        }

        private void OnBattleDisconnected(Connect connect)
        {
            if (connect != battleConnect)
            {
                return;
            }

            battleConnect = null;
            if (battleReconnectTimerId != 0)
            {
                return;
            }

            battleReconnectTimerId = NetworkTimer.Instance.AddRepeated(
                ServerUtility.BattleLobbyReconnectInterval,
                () =>
                {
                    if (battleConnect == null)
                    {
                        ConnectBattle();
                    }
                });
        }
        #region Server
        private void OnDisconnected(Connect connect)
        {
            if (pendingConnectAccountDic.TryGetValue(connect, out ulong pendingAccountId))
            {
                pendingConnectAccountDic.Remove(connect);
                pendingPlayerList.Remove(pendingAccountId);
                connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);
                return;
            }

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
                        room.players[room.ownerAccountId].ready = false;
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
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_SetDungeonTheme>(), OnClientSetDungeonTheme);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_Start>(), OnClientStart);
        }
        private void OnAccept(Connect connect)
        {
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);
        }
        private void OnClientLogin(IMessage message, Connect connect)
        {
            C2L_LoginLobby loginMessage = message as C2L_LoginLobby;
            if (loginMessage == null)
            {
                return;
            }

            if (loginMessage.accountId == 0UL ||
                !Guid.TryParse(loginMessage.saveGuid, out Guid saveGuid) ||
                connectAccountDic.ContainsKey(connect) ||
                pendingConnectAccountDic.ContainsKey(connect) ||
                playerList.ContainsKey(loginMessage.accountId) ||
                pendingPlayerList.ContainsKey(loginMessage.accountId))
            {
                connect.Send(new L2C_LoginLobbyResult { type = LobbyRequestType.LoginFail });
                lobbyService.DisconnectAfterSend(connect);
                return;
            }

            Player player = new Player
            {
                accountId = loginMessage.accountId,
                username = loginMessage.username,
                saveGuid = saveGuid.ToString("N"),
                roomId = 0UL,
                connect = connect,
            };
            pendingConnectAccountDic.Add(connect, player.accountId);
            pendingPlayerList.Add(player.accountId, player);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);

            if (battleConnect != null && battleConnect.State == ConnectState.Connected)
            {
                battleConnect.Send(new L2B_TryReloadRoom
                {
                    accountId = player.accountId,
                    saveGuid = player.saveGuid,
                });
            }
        }
        private bool CreatePlayer(Player player)
        {
            if (player == null || player.accountId == 0UL || player.connect == null ||
                !Guid.TryParse(player.saveGuid, out Guid saveGuid) ||
                connectAccountDic.ContainsKey(player.connect) ||
                playerList.ContainsKey(player.accountId))
            {
                return false;
            }

            player.saveGuid = saveGuid.ToString("N");
            pendingConnectAccountDic.Remove(player.connect);
            pendingPlayerList.Remove(player.accountId);
            connectAccountDic.Add(player.connect, player.accountId);
            playerList.Add(player.accountId, player);

            player.connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_CreateRoom>(), OnClientCreateRoom);
            player.connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_JoinRoom>(), OnClientJoinRoom);
            player.connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_LeaveRoom>(), OnClientLeaveRoom);
            player.connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_Ready>(), OnClientReady);
            player.connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_SetDungeonTheme>(), OnClientSetDungeonTheme);
            player.connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_Start>(), OnClientStart);

            player.connect.Send(new L2C_RefreshRoomList
            {
                roomListData = RoomListData.CreateRoomData(roomList)
            });
            return true;
        }
        private void OnClientStart(IMessage message, Connect connect)
        {
            if (!connectAccountDic.TryGetValue(connect, out var accountId)
                || !playerList.TryGetValue(accountId, out Player player)
                || !roomList.TryGetValue(player.roomId, out var room)
                || battleConnect == null
                || battleConnect.State != ConnectState.Connected)
            {
                connect.Send(new L2C_StartReturn { type = LobbyRequestType.StartFail });
                return;
            }

            if (room.ownerAccountId != accountId || room.start)
            {
                connect.Send(new L2C_StartReturn { type = LobbyRequestType.StartFail });
                return;
            }

            foreach (var p in room.players)
            {
                if (p.Key == room.ownerAccountId)
                    continue;

                if (p.Value.ready)
                    continue;

                connect.Send(new L2C_StartReturn { type = LobbyRequestType.StartFail });
                return;
            }

            room.start = true;
            RoomData roomData = RoomData.CreateRoomData(room);
            foreach (Player member in room.players.Values)
            {
                member.connect.Send(new L2C_RefreshRoomInfo { roomData = roomData });
            }

            battleConnect.Send(new L2B_StartRoom()
            {
                roomId = room.roomId,
                ownerAccountId = room.ownerAccountId,
                themeKey = room.themeKey,
                players = room.players.Keys.ToArray(),
                saveGuids = room.players.ToDictionary(pair => pair.Key, pair => pair.Value.saveGuid),
            });
        }
        private void OnClientReady(IMessage message, Connect connect)
        {
            C2L_Ready realMessage = message as C2L_Ready;
            if (realMessage == null
                || !connectAccountDic.TryGetValue(connect, out ulong accountId)
                || !playerList.TryGetValue(accountId, out Player player)
                || !roomList.TryGetValue(player.roomId, out Room room)
                || room.start
                || room.ownerAccountId == accountId)
            {
                return;
            }

            player.ready = realMessage.ready;

            RoomData roomData = RoomData.CreateRoomData(room);
            foreach (var p in room.players)
            {
                p.Value.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
            }
        }
        private void OnClientSetDungeonTheme(IMessage message, Connect connect)
        {
            C2L_SetDungeonTheme realMessage = message as C2L_SetDungeonTheme;
            if (realMessage != null
                && connectAccountDic.TryGetValue(connect, out ulong accountId)
                && playerList.TryGetValue(accountId, out Player player)
                && roomList.TryGetValue(player.roomId, out Room room)
                && room.ownerAccountId == accountId
                && !room.start)
            {
                room.themeKey = Math.Max(0, realMessage.themeKey);

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
                room.themeKey = 0;
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
            if (room.start)
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
                || !roomList.TryGetValue(player.roomId, out Room room)
                || room.start
                || player.ready)
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
                    room.players[room.ownerAccountId].ready = false;
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
        public void Update()
        {
            lobbyService.Update();
            clientService.Update();
        }

        public void Cleanup()
        {
            if (battleReconnectTimerId != 0)
            {
                NetworkTimer.Instance.Remove(battleReconnectTimerId);
                battleReconnectTimerId = 0;
            }

            if (battleConnect != null)
            {
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2L_StartRoomResult>(), OnBattleStart);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2L_ReloadRoomResult>(), OnBattleReload);
                battleConnect.OnConnected -= OnBattleConnected;
                battleConnect.OnDisconnected -= OnBattleDisconnected;
            }

            lobbyService?.Shutdown();
            clientService?.Shutdown();

            roomList.Clear();
            playerList.Clear();
            connectAccountDic.Clear();
            pendingPlayerList.Clear();
            pendingConnectAccountDic.Clear();
            battleConnect = null;
            lobbyService = null;
            clientService = null;
        }
        #endregion
        #region Client
        public void OnBattleStart(IMessage message, Connect connect)
        {
            B2L_StartRoomResult realMessage = message as B2L_StartRoomResult;
            if (realMessage == null || connect != battleConnect
                || !roomList.TryGetValue(realMessage.roomId, out Room room))
            {
                return;
            }

            Dictionary<ulong, string> secretKeys = realMessage.secretKeys ?? new Dictionary<ulong, string>();
            Player[] roomPlayers = room.players.Values.ToArray();
            foreach (Player player in roomPlayers)
            {
                if (player == null)
                    continue;

                if (player.connect != null)
                {
                    if (secretKeys.TryGetValue(player.accountId, out string ticket)
                        && !string.IsNullOrEmpty(ticket))
                    {
                        player.connect.Send(new L2C_StartTicket { ticket = ticket, reload = false });
                    }

                    lobbyService.DisconnectAfterSend(player.connect);
                    connectAccountDic.Remove(player.connect);
                }

                playerList.Remove(player.accountId);
                player.roomId = 0UL;
                player.connect = null;
            }

            room.players.Clear();
            roomList.Remove(realMessage.roomId);
            BroadcastRoomListChanged();
        }
        public void OnBattleReload(IMessage message, Connect connect)
        {
            B2L_ReloadRoomResult realMessage = message as B2L_ReloadRoomResult;
            if (realMessage == null || connect != battleConnect || realMessage.accountId == 0UL)
            {
                return;
            }

            if (pendingPlayerList.TryGetValue(realMessage.accountId, out Player pendingPlayer))
            {
                if (!string.Equals(pendingPlayer.saveGuid, realMessage.saveGuid, StringComparison.Ordinal))
                {
                    return;
                }

                if (realMessage.type == BattleReloadRoomResultType.NoBattle)
                {
                    if (!CreatePlayer(pendingPlayer))
                    {
                        pendingPlayerList.Remove(pendingPlayer.accountId);
                        pendingConnectAccountDic.Remove(pendingPlayer.connect);
                        pendingPlayer.connect.Send(new L2C_LoginLobbyResult { type = LobbyRequestType.LoginFail });
                        lobbyService.DisconnectAfterSend(pendingPlayer.connect);
                    }
                    return;
                }

                pendingPlayerList.Remove(pendingPlayer.accountId);
                pendingConnectAccountDic.Remove(pendingPlayer.connect);
                if (realMessage.type == BattleReloadRoomResultType.ReloadAvailable &&
                    !string.IsNullOrEmpty(realMessage.ticket))
                {
                    pendingPlayer.connect.Send(new L2C_StartTicket
                    {
                        ticket = realMessage.ticket,
                        reload = true,
                    });
                }
                else
                {
                    pendingPlayer.connect.Send(new L2C_LoginLobbyResult
                    {
                        type = realMessage.type == BattleReloadRoomResultType.SaveMismatch
                            ? LobbyRequestType.LoginSaveMismatch
                            : LobbyRequestType.LoginFail,
                    });
                }

                lobbyService.DisconnectAfterSend(pendingPlayer.connect);
                return;
            }

            if (!playerList.TryGetValue(realMessage.accountId, out Player player) ||
                player.connect == null ||
                !string.Equals(player.saveGuid, realMessage.saveGuid, StringComparison.Ordinal) ||
                realMessage.type == BattleReloadRoomResultType.NoBattle)
            {
                return;
            }

            if (realMessage.type == BattleReloadRoomResultType.ReloadAvailable &&
                !string.IsNullOrEmpty(realMessage.ticket))
            {
                player.connect.Send(new L2C_StartTicket { ticket = realMessage.ticket, reload = true });
                lobbyService.DisconnectAfterSend(player.connect);
                playerList.Remove(realMessage.accountId);
                connectAccountDic.Remove(player.connect);
                player.connect = null;
                return;
            }

            player.connect.Send(new L2C_LoginLobbyResult
            {
                type = realMessage.type == BattleReloadRoomResultType.SaveMismatch
                    ? LobbyRequestType.LoginSaveMismatch
                    : LobbyRequestType.LoginFail,
            });
            lobbyService.DisconnectAfterSend(player.connect);
        }
        #endregion
    }
}
