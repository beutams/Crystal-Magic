using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using System;
using UnityEngine;

namespace Server
{
    public class ClientLobbyManager
    {
        public ClientService clientServic => ClientNetworkManager.Instance.clientServic;
        public ulong accountId;
        public RoomListData roomList;
        public RoomData room;

        protected Connect lobbyConnect;
        protected LobbyRequest request;
        protected bool disconnecting;
        private bool loggedIn;

        public Action<RoomListData> onRoomRefresh;
        public Action<RoomData> onRoomInfoRefresh;
        public Action onLeaveRoom;
        public Action<LobbyRequestType> onRequestWaiting;
        public Action onRequestFinished;
        public Action<LobbyRequestType> onRequestFail;
        public void Initialize()
        {
            clientServic.Connect(ServerUtility.GetLobbyIPEndPoint(), out lobbyConnect);

            lobbyConnect.OnConnected += OnLobbyConnected;
            lobbyConnect.OnDisconnected += OnDisconnected;

            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_StartTicket>(), OnStartTicket);
        }
        public void Cleanup()
        {
            if (request != null)
            {
                NetworkTimer.Instance.Remove(request.waitTimerId);
                NetworkTimer.Instance.Remove(request.timeoutTimerId);
                request = null;
            }

            if (lobbyConnect != null)
            {
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_StartTicket>(), OnStartTicket);
                lobbyConnect.OnConnected -= OnLobbyConnected;
                lobbyConnect.OnDisconnected -= OnDisconnected;
                clientServic.Disconnect(lobbyConnect);
                lobbyConnect = null;
            }

            loggedIn = false;
            disconnecting = false;
            accountId = 0UL;
            room = null;
            roomList = null;
        }
        private void OnLobbyConnected(Connect connect)
        {
            if (connect != lobbyConnect)
            {
                return;
            }

            LobbyAccountConfig config = ConfigComponent.Instance.Get<LobbyAccountConfig>();
            accountId = config.accountId;
            if (accountId == 0UL)
            {
                Debug.LogError("[Lobby] Lobby account ID must not be zero.");
                clientServic.Disconnect(lobbyConnect);
                return;
            }

            lobbyConnect.Send(new C2L_LoginLobby
            {
                accountId = accountId,
                username = config.username,
            });
        }
        private void OnDisconnected(Connect connect)
        {
            if (connect != lobbyConnect)
            {
                return;
            }

            disconnecting = true;
            loggedIn = false;
            accountId = 0UL;
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_StartTicket>(), OnStartTicket);
            lobbyConnect.OnConnected -= OnLobbyConnected;
            lobbyConnect.OnDisconnected -= OnDisconnected;

            if (request != null)
            {
                NetworkTimer.Instance.Remove(request.waitTimerId);
                NetworkTimer.Instance.Remove(request.timeoutTimerId);
                request = null;
                onRequestFinished?.Invoke();
            }

            room = null;
            roomList = new RoomListData();
            onRoomRefresh?.Invoke(roomList);
            onRoomInfoRefresh?.Invoke(null);
        }
        private void OnRefreshRoomInfo(IMessage message, Connect connect)
        {
            L2C_RefreshRoomInfo roomData = message as L2C_RefreshRoomInfo;
            if(roomData != null)
            {
                room = roomData.roomData;
                onRoomInfoRefresh?.Invoke(room);
            }
        }
        private void OnStartTicket(IMessage message, Connect connect)
        {
            L2C_StartTicket startTicket = message as L2C_StartTicket;
            if (connect == lobbyConnect && startTicket != null)
            {
                FinishRequest(LobbyRequestType.StartRequest);
                ClientNetworkManager.Instance.clientBattleManager.ConnectWithTicket(startTicket.ticket);
            }
        }
        public void OnRefreshRoomList(IMessage message,Connect connect)
        {
            L2C_RefreshRoomList roomListData = message as L2C_RefreshRoomList;
            if (roomListData != null && roomListData.roomListData != null)
            {
                if (!loggedIn)
                {
                    loggedIn = true;
                }
                roomList = roomListData.roomListData;
                onRoomRefresh?.Invoke(roomList);
            }
        }
        public void JoinRoom(ulong roomId)
        {
            if (!BeginRequest(LobbyRequestType.JoinRequest))
            {
                return;
            }

            lobbyConnect.Send(new C2L_JoinRoom { roomId = roomId });
        }
        public void OnJoinRoom(IMessage message, Connect connect)
        {
            L2C_JoinReturn joinReturn = message as L2C_JoinReturn;
            if (joinReturn == null) return;

            if (!FinishRequest(LobbyRequestType.JoinRequest))
            {
                return;
            }

            switch (joinReturn.type)
            {
                case LobbyRequestType.JoinSuccess:
                    room = joinReturn.roomData;
                    onRoomInfoRefresh?.Invoke(room);
                    break;
                case LobbyRequestType.JoinFail:
                case LobbyRequestType.JoinRoomFull:
                case LobbyRequestType.JoinRoomClosed:
                    onRequestFail?.Invoke(joinReturn.type);
                    break;
            }
        }
        public void CreateRoom(string name)
        {
            if (!BeginRequest(LobbyRequestType.CreateRequest))
            {
                return;
            }

            lobbyConnect.Send(new C2L_CreateRoom() { roomName = name });
        }
        public void OnCreateRoom(IMessage message, Connect connect)
        {
            L2C_CreateReturn createReturn = message as L2C_CreateReturn;
            if (createReturn == null) return;

            if (!FinishRequest(LobbyRequestType.CreateRequest))
            {
                return;
            }

            switch (createReturn.type)
            {
                case LobbyRequestType.CreateSuccess:
                    room = createReturn.roomData;
                    onRoomInfoRefresh?.Invoke(room);
                    break;
                case LobbyRequestType.CreateFail:
                    onRequestFail?.Invoke(createReturn.type);
                    break;
            }
        }
        public void LeaveRoom()
        {
            if (!BeginRequest(LobbyRequestType.LeaveRequest))
            {
                return;
            }

            lobbyConnect.Send(new C2L_LeaveRoom());
        }
        public void OnLeaveRoom(IMessage message, Connect connect)
        {
            L2C_LeaveReturn leaveReturn = message as L2C_LeaveReturn;
            if (leaveReturn == null) return;

            if (!FinishRequest(LobbyRequestType.LeaveRequest))
            {
                return;
            }

            switch (leaveReturn.type)
            {
                case LobbyRequestType.LeaveSuccess:
                    room = null;
                    onRoomInfoRefresh?.Invoke(null);
                    onLeaveRoom?.Invoke();
                    break;
                case LobbyRequestType.LeaveFail:
                    onRequestFail?.Invoke(leaveReturn.type);
                    break;
            }
        }
        public void Ready(bool ready)
        {
            if (!loggedIn || disconnecting || request != null || room == null)
            {
                return;
            }
            lobbyConnect.Send(new C2L_Ready() { ready = ready});
        }
        public void SetDungeonFloor(int dungeonFloor)
        {
            if (!loggedIn || disconnecting || request != null || room == null || room.ownerAccountId != accountId)
            {
                return;
            }
            lobbyConnect.Send(new C2L_SetDungeonFloor() { dungeonFloor = Math.Max(1, dungeonFloor) });
        }
        public void StartRoom()
        {
            if (!BeginRequest(LobbyRequestType.StartRequest))
            {
                return;
            }
            lobbyConnect.Send(new C2L_Start());
        }
        public void OnStartResult(IMessage message, Connect connect)
        {

        }
        private bool BeginRequest(LobbyRequestType type)
        {
            if (!loggedIn || disconnecting || request != null)
            {
                return false;
            }

            request = new LobbyRequest();
            request.type = type;

            LobbyRequest currentRequest = request;
            request.waitTimerId = NetworkTimer.Instance.AddOnce(500, () =>
            {
                if (request == currentRequest)
                {
                    onRequestWaiting?.Invoke(request.type);
                }
            });
            request.timeoutTimerId = NetworkTimer.Instance.AddOnce(5000, () =>
            {
                OnRequestTimeout(currentRequest);
            });

            return true;
        }
        private bool FinishRequest(LobbyRequestType type)
        {
            if (request == null || request.type != type)
            {
                return false;
            }

            NetworkTimer.Instance.Remove(request.waitTimerId);
            NetworkTimer.Instance.Remove(request.timeoutTimerId);
            request = null;
            onRequestFinished?.Invoke();
            return true;
        }
        private void OnRequestTimeout(LobbyRequest timeoutRequest)
        {
            if (request != timeoutRequest)
            {
                return;
            }

            disconnecting = true;
            LobbyRequestType type = request.type;
            FinishRequest(type);

            switch (type)
            {
                case LobbyRequestType.CreateRequest:
                    onRequestFail?.Invoke(LobbyRequestType.CreateTimeout);
                    break;
                case LobbyRequestType.JoinRequest:
                    onRequestFail?.Invoke(LobbyRequestType.JoinTimeout);
                    break;
                case LobbyRequestType.LeaveRequest:
                    onRequestFail?.Invoke(LobbyRequestType.LeaveTimeout);
                    break;
                case LobbyRequestType.StartRequest:
                    onRequestFail?.Invoke(LobbyRequestType.StartTimeout);
                    break;
            }

            clientServic.Disconnect(lobbyConnect);
        }
    }
}
