using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.UI;
using System;
using UnityEngine;

namespace Server
{
    public class ClientLobbyManager
    {
        public ClientService clientServic => NetworkComponent.Instance.clientServic;
        public ulong accountId;
        public RoomListData roomList;
        public RoomData room;

        protected Connect lobbyConnect;
        protected LobbyRequest request;
        protected bool disconnecting;
        private bool loggedIn;

        public bool HasConnection => lobbyConnect != null;
        public bool DisconnectRequested { get; private set; }
        public LobbyRequestType LoginFailure { get; private set; }

        public Action<RoomListData> onRoomRefresh;
        public Action<RoomData> onRoomInfoRefresh;
        public Action onLeaveRoom;
        public Action<LobbyRequestType> onRequestWaiting;
        public Action onRequestFinished;
        public Action<LobbyRequestType> onRequestFail;
        public Action onDisconnected;
        public void Initialize()
        {
            if (lobbyConnect != null)
            {
                return;
            }

            LoginFailure = LobbyRequestType.Unknown;
            clientServic.Connect(ServerUtility.GetLobbyIPEndPoint(), out lobbyConnect);

            lobbyConnect.OnConnected += OnLobbyConnected;
            lobbyConnect.OnDisconnected += OnDisconnected;

            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_LoginLobbyResult>(), OnLoginResult);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_StartReturn>(), OnStartResult);
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
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LoginLobbyResult>(), OnLoginResult);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_StartReturn>(), OnStartResult);
                lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_StartTicket>(), OnStartTicket);
                lobbyConnect.OnConnected -= OnLobbyConnected;
                lobbyConnect.OnDisconnected -= OnDisconnected;
                clientServic.Disconnect(lobbyConnect);
                lobbyConnect = null;
            }

            loggedIn = false;
            disconnecting = false;
            DisconnectRequested = false;
            LoginFailure = LobbyRequestType.Unknown;
            accountId = 0UL;
            room = null;
            roomList = null;
        }
        public void Disconnect()
        {
            if (lobbyConnect == null)
            {
                return;
            }

            DisconnectRequested = true;
            disconnecting = true;
            clientServic.Disconnect(lobbyConnect);
        }
        private void OnLobbyConnected(Connect connect)
        {
            if (connect != lobbyConnect)
            {
                return;
            }

            disconnecting = false;
            DisconnectRequested = false;
            LobbyAccountConfig config = ConfigComponent.Instance.Get<LobbyAccountConfig>();
            accountId = config.accountId;
            if (accountId == 0UL)
            {
                Debug.LogError("[Lobby] Lobby account ID must not be zero.");
                clientServic.Disconnect(lobbyConnect);
                return;
            }

            string saveGuid = SaveDataComponent.Instance.CurrentSaveGuid;
            if (!Guid.TryParse(saveGuid, out Guid parsedSaveGuid))
            {
                LoginFailure = LobbyRequestType.LoginFail;
                Debug.LogError("[Lobby] A loaded save with a valid SaveGuid is required.");
                clientServic.Disconnect(lobbyConnect);
                return;
            }

            lobbyConnect.Send(new C2L_LoginLobby
            {
                accountId = accountId,
                username = config.username,
                saveGuid = parsedSaveGuid.ToString("N"),
            });
        }
        private void OnLoginResult(IMessage message, Connect connect)
        {
            L2C_LoginLobbyResult result = message as L2C_LoginLobbyResult;
            if (connect != lobbyConnect || result == null)
            {
                return;
            }

            LoginFailure = result.type;
        }
        private void OnDisconnected(Connect connect)
        {
            if (connect != lobbyConnect)
            {
                return;
            }

            bool disconnectRequested = DisconnectRequested;
            disconnecting = true;
            loggedIn = false;
            accountId = 0UL;
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LoginLobbyResult>(), OnLoginResult);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_StartReturn>(), OnStartResult);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_StartTicket>(), OnStartTicket);
            lobbyConnect.OnConnected -= OnLobbyConnected;
            lobbyConnect.OnDisconnected -= OnDisconnected;
            lobbyConnect = null;

            if (request != null)
            {
                NetworkTimer.Instance.Remove(request.waitTimerId);
                NetworkTimer.Instance.Remove(request.timeoutTimerId);
                request = null;
                onRequestFinished?.Invoke();
            }

            room = null;
            roomList = new RoomListData();
            disconnecting = false;
            DisconnectRequested = disconnectRequested;
            onRoomRefresh?.Invoke(roomList);
            onRoomInfoRefresh?.Invoke(null);
            onDisconnected?.Invoke();
        }
        private void OnRefreshRoomInfo(IMessage message, Connect connect)
        {
            L2C_RefreshRoomInfo roomData = message as L2C_RefreshRoomInfo;
            if(connect == lobbyConnect && roomData != null)
            {
                room = roomData.roomData;
                onRoomInfoRefresh?.Invoke(room);
            }
        }
        private void OnStartTicket(IMessage message, Connect connect)
        {
            L2C_StartTicket startTicket = message as L2C_StartTicket;
            if (connect != lobbyConnect || startTicket == null || string.IsNullOrEmpty(startTicket.ticket))
            {
                return;
            }

            if (request != null)
            {
                NetworkTimer.Instance.Remove(request.waitTimerId);
                NetworkTimer.Instance.Remove(request.timeoutTimerId);
                request = null;
                onRequestFinished?.Invoke();
            }

            ulong localAccountId = accountId;
            ClientBattleManager battleManager = NetworkComponent.Instance.clientBattleManager;
            string error = null;
            if (battleManager == null || !battleManager.PrepareForOnlineBattle(out error))
            {
                UIComponent.Instance.Open<ConfirmSingleUI>(new ConfirmUIOpenData(
                    "战斗准备失败",
                    string.IsNullOrEmpty(error) ? "无法保存当前角色数据。" : error,
                    null,
                    null,
                    "确定",
                    false));
                Disconnect();
                return;
            }

            GameFlowComponent.Instance.BeginTransition(OnlineBattlePreparationState.CreateEnterTransitionData(
                battleManager,
                startTicket.ticket,
                localAccountId,
                startTicket.reload));
            Disconnect();
        }
        public void OnRefreshRoomList(IMessage message,Connect connect)
        {
            L2C_RefreshRoomList roomListData = message as L2C_RefreshRoomList;
            if (connect == lobbyConnect && roomListData != null && roomListData.roomListData != null)
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
            if (roomId == 0UL || room != null || !BeginRequest(LobbyRequestType.JoinRequest))
            {
                return;
            }

            lobbyConnect.Send(new C2L_JoinRoom { roomId = roomId });
        }
        public void OnJoinRoom(IMessage message, Connect connect)
        {
            L2C_JoinReturn joinReturn = message as L2C_JoinReturn;
            if (joinReturn == null) return;

            if (!FinishRequest(LobbyRequestType.JoinRequest, connect))
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
            if (room != null || !BeginRequest(LobbyRequestType.CreateRequest))
            {
                return;
            }

            lobbyConnect.Send(new C2L_CreateRoom() { roomName = name });
        }
        public void OnCreateRoom(IMessage message, Connect connect)
        {
            L2C_CreateReturn createReturn = message as L2C_CreateReturn;
            if (createReturn == null) return;

            if (!FinishRequest(LobbyRequestType.CreateRequest, connect))
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
            if (room == null
                || room.start
                || room.playerready.TryGetValue(accountId, out bool ready) && ready
                || !BeginRequest(LobbyRequestType.LeaveRequest))
            {
                return;
            }

            lobbyConnect.Send(new C2L_LeaveRoom());
        }
        public void OnLeaveRoom(IMessage message, Connect connect)
        {
            L2C_LeaveReturn leaveReturn = message as L2C_LeaveReturn;
            if (leaveReturn == null) return;

            if (!FinishRequest(LobbyRequestType.LeaveRequest, connect))
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
            if (!loggedIn || disconnecting || request != null || room == null
                || room.start || room.ownerAccountId == accountId
                || !room.players.ContainsKey(accountId))
            {
                return;
            }
            lobbyConnect.Send(new C2L_Ready() { ready = ready});
        }
        public void SetDungeonTheme(int themeKey)
        {
            if (!loggedIn || disconnecting || request != null || room == null
                || room.start || room.ownerAccountId != accountId)
            {
                return;
            }
            lobbyConnect.Send(new C2L_SetDungeonTheme() { themeKey = Math.Max(0, themeKey) });
        }
        public void StartRoom()
        {
            if (room == null || room.start || room.ownerAccountId != accountId
                || !BeginRequest(LobbyRequestType.StartRequest))
            {
                return;
            }
            lobbyConnect.Send(new C2L_Start());
        }
        public void OnStartResult(IMessage message, Connect connect)
        {
            L2C_StartReturn startReturn = message as L2C_StartReturn;
            if (startReturn == null || !FinishRequest(LobbyRequestType.StartRequest, connect))
            {
                return;
            }

            if (startReturn.type != LobbyRequestType.StartSuccess)
                onRequestFail?.Invoke(startReturn.type);
        }
        private bool BeginRequest(LobbyRequestType type)
        {
            if (!loggedIn || disconnecting || request != null)
            {
                return false;
            }

            request = new LobbyRequest
            {
                type = type,
                connect = lobbyConnect,
            };

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
        private bool FinishRequest(LobbyRequestType type, Connect connect)
        {
            if (request == null || request.type != type || request.connect != connect)
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
            if (request != timeoutRequest || timeoutRequest.connect != lobbyConnect)
            {
                return;
            }

            LobbyRequestType timeoutType = timeoutRequest.type;
            if (!FinishRequest(timeoutType, timeoutRequest.connect))
            {
                return;
            }

            switch (timeoutType)
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
        }
    }
}
