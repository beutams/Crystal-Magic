using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class ServerLobbyManager : Singleton<ServerLobbyManager>
    {
        public ServerService lobbyService;
        public Dictionary<long, Room> roomList = new Dictionary<long, Room>();
        public Dictionary<long, Player> playerList = new Dictionary<long, Player>();
        public Dictionary<Connect, long> connectPlayerDic = new Dictionary<Connect, long>();
        public long nextRoomId;
        //临时
        public long nextUserId = 111;
        private Player CreateNewPlayer()
        {
            Player player = new Player();
            player.userId = nextUserId;
            player.roomId = -1;
            player.username = "测试名称";
            player.start = false;
            nextUserId++;
            return player;
        }


        protected override void Awake()
        {
            base.Awake();
            lobbyService = new ServerService();
            lobbyService.OnAccept += OnAccept;
            lobbyService.OnDisconnected += OnDisconnected;

            lobbyService.Init();
        }
        private void OnDisconnected(Connect connect)
        {
            long playerId = connectPlayerDic[connect];
            Player player = playerList[playerId];
            if(player.roomId != -1)
            {
                Room room = roomList[player.roomId];
                room.players.Remove(playerId);
                room.enterNum = room.players.Count;
                if (room.players.Count != 0)
                {
                    if (room.ownerId == playerId)
                    {
                        room.ownerId = room.players.First().Key;

                        RoomData roomData = new RoomData();
                        roomData.roomId = room.roomId;
                        roomData.roomName = room.roomName;
                        roomData.enterNum = room.enterNum;
                        roomData.maxNum = room.maxNum;
                        roomData.ownerId = room.ownerId;
                        foreach (var item in room.players)
                        {
                            roomData.players.Add(item.Key, item.Value.username);
                        }
                        foreach (var allPlayer in room.players)
                        {
                            allPlayer.Value.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
                        }
                    }
                    else
                    {
                        RoomData roomData = RoomData.CreateRoomData(room);
                        foreach (Player member in room.players.Values)
                        {
                            member.connect.Send(new L2C_RefreshRoomInfo
                            {
                                roomData = roomData
                            });
                        }
                    }
                }
                else
                {
                    roomList.Remove(player.roomId);
                }
            }
            connectPlayerDic.Remove(connect);
            playerList.Remove(playerId);
            BroadcastRoomListChanged();

            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_CreateRoom>(), OnClientCreateRoom);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_JoinRoom>(), OnClientJoinRoom);
            connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2L_LeaveRoom>(), OnClientLeaveRoom);
        }
        private void OnAccept(Connect connect)
        {
            Player player = CreateNewPlayer();
            player.connect = connect;
            connectPlayerDic.Add(connect, player.userId);
            playerList.Add(player.userId, player);

            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_LoginLobby>(), OnClientLogin);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_CreateRoom>(), OnClientCreateRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_JoinRoom>(), OnClientJoinRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2L_LeaveRoom>(), OnClientLeaveRoom);

            connect.Send(new L2C_RefreshRoomList { roomListData = RoomListData.CreateRoomData(roomList) });
        }
        private void OnClientLogin(IMessage message, Connect connect)
        {
            connect.Send(new L2C_RefreshRoomList() { roomListData = RoomListData.CreateRoomData(roomList) });
        }
        private void OnClientCreateRoom(IMessage message, Connect connect)
        {
            C2L_CreateRoom realMessage = message as C2L_CreateRoom;
            if (connectPlayerDic.TryGetValue(connect, out long playerId) && playerList.TryGetValue(playerId, out Player player) && player.roomId == -1)
            {
                Room room = new Room();
                room.roomId = GetNewRoomId();
                room.ownerId = playerId;
                room.roomName = realMessage.roomName;
                room.enterNum = 1;
                room.maxNum = 4;
                room.start = false;
                room.players.Add(player.userId, player);

                player.roomId = room.roomId;

                RoomData roomData = RoomData.CreateRoomData(room);

                roomList.Add(room.roomId, room);

                connect.Send(new L2C_CreateReturn() { success = true, roomData = roomData });
            }
            BroadcastRoomListChanged();
        }
        private void OnClientJoinRoom(IMessage message, Connect connect)
        {
            C2L_JoinRoom data = message as C2L_JoinRoom;
            if (!roomList.TryGetValue(data.roomId, out Room room)
                || room.players.Count >= room.maxNum
                || !connectPlayerDic.TryGetValue(connect, out long playerId)
                || !playerList.TryGetValue(playerId, out Player player)
                || player.roomId != -1)
            {
                connect.Send(new L2C_JoinReturn() { success = false });
                return;
            }

            player.roomId = room.roomId;

            room.players.Add(playerId, player);
            room.enterNum = room.players.Count;

            RoomData roomData = RoomData.CreateRoomData(room);

            connect.Send(new L2C_JoinReturn() { success = true, roomData = roomData });
            foreach (var elsePlayer in room.players)
            {
                if (elsePlayer.Key == playerId)
                    continue;

                elsePlayer.Value.connect.Send(new L2C_RefreshRoomInfo() { roomData = roomData });
            }
            BroadcastRoomListChanged();
        }
        private void OnClientLeaveRoom(IMessage message, Connect connect)
        {
            if (!connectPlayerDic.TryGetValue(connect, out long playerId)
                || !playerList.TryGetValue(playerId, out Player player)
                || player.roomId == -1
                || !roomList.TryGetValue(player.roomId, out Room room))
            {
                connect.Send(new L2C_LeaveReturn() { success = false });
                return;
            }
            player.roomId = -1;
            room.players.Remove(playerId);
            room.enterNum = room.players.Count;

            RoomData roomData = RoomData.CreateRoomData(room);
            connect.Send(new L2C_LeaveReturn() { success = true });

            if(room.players.Count == 0)
            {
                roomList.Remove(room.roomId);
            }
            else
            {
                foreach (var elsePlayer in room.players)
                {
                    if (elsePlayer.Key == playerId)
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
        private long GetNewRoomId()
        {
            return nextRoomId++;
        }
        private void LateUpdate()
        {
            lobbyService.Update();
        }
    }
}