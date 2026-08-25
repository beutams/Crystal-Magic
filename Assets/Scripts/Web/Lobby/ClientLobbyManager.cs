using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class ClientLobbyManager : Singleton<ClientLobbyManager>
    {
        public ClientService clientServic;
        public RoomListData roomList;
        public RoomData room;

        protected Connect lobbyConnect;

        public Action<RoomListData> onRoomRefresh;
        public Action<RoomData> onRoomInfoRefresh;
        public Action onLeaveRoom;
        public Action<LobbyRequestType> onRequestFail;
        protected override void Awake()
        {
            base.Awake();
            clientServic = new ClientService();
            clientServic.Connect(ServerUtility.GetLobbyIPEndPoint(),out lobbyConnect);

            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
        }
        public void OnDisconnected()
        {
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomInfo>(), OnRefreshRoomInfo);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_JoinReturn>(), OnJoinRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_CreateReturn>(), OnCreateRoom);
            lobbyConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<L2C_LeaveReturn>(), OnLeaveRoom);
        }
        private void OnRefreshRoomInfo(IMessage message, Connect connect)
        {
            L2C_RefreshRoomInfo roomData = message as L2C_RefreshRoomInfo;
            if(roomData != null)
            {
                onRoomInfoRefresh?.Invoke(roomData.roomData);
            }
        }
        public void OnRefreshRoomList(IMessage message,Connect connect)
        {
            L2C_RefreshRoomList roomListData = message as L2C_RefreshRoomList;
            if (roomListData != null && roomListData.roomListData != null)
            {
                roomList = roomListData.roomListData;
                onRoomRefresh?.Invoke(roomList);
            }
        }
        public void JoinRoom(long roomId)
        {
            lobbyConnect.Send(new C2L_JoinRoom { roomId = roomId });
        }
        public void OnJoinRoom(IMessage message, Connect connect)
        {
            L2C_JoinReturn joinReturn = message as L2C_JoinReturn;
            if (joinReturn == null) return;
            switch (joinReturn.type)
            {
                case LobbyRequestType.Success:
                    room = joinReturn.roomData;
                    onRoomInfoRefresh?.Invoke(room);
                    break;
                case LobbyRequestType.Fail:
                    onRequestFail?.Invoke(LobbyRequestType.Fail);
                    break;
                case LobbyRequestType.RoomFull:
                    onRequestFail?.Invoke(LobbyRequestType.RoomFull);
                    break;
                case LobbyRequestType.RoomClosed:
                    onRequestFail?.Invoke(LobbyRequestType.RoomClosed);
                    break;
            }
        }
        public void CreateRoom(string name)
        {
            lobbyConnect.Send(new C2L_CreateRoom() { roomName = name });
        }
        public void OnCreateRoom(IMessage message, Connect connect)
        {
            L2C_CreateReturn createReturn = message as L2C_CreateReturn;
            if (createReturn == null) return;
            switch (createReturn.type)
            {
                case LobbyRequestType.Success:
                    room = createReturn.roomData;
                    onRoomInfoRefresh?.Invoke(room);
                    break;
                case LobbyRequestType.Fail:
                    onRequestFail?.Invoke(LobbyRequestType.Fail);
                    break;
            }
        }
        public void LeaveRoom(string name)
        {
            lobbyConnect.Send(new C2L_LeaveRoom());
        }
        public void OnLeaveRoom(IMessage message, Connect connect)
        {
            L2C_LeaveReturn createReturn = message as L2C_LeaveReturn;
            if (createReturn == null) return;
            switch (createReturn.type)
            {
                case LobbyRequestType.Success:
                    onLeaveRoom?.Invoke();
                    break;
                case LobbyRequestType.Fail:
                    onRequestFail?.Invoke(LobbyRequestType.Fail);
                    break;
            }
        }
    }
}