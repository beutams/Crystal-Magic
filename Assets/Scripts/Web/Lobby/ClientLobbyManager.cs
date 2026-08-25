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
        public Action<bool,RoomData> onRoomInfoRefresh;
        protected override void Awake()
        {
            base.Awake();
            clientServic = new ClientService();
            clientServic.Connect(ServerUtility.GetLobbyIPEndPoint(),out lobbyConnect);

            lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2C_RefreshRoomList>(), OnRefreshRoomList);
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
            if(joinReturn.success)
            {
                room = joinReturn.roomData;
                onRoomInfoRefresh?.Invoke(true,room);
            }
            else
            {
                onRoomInfoRefresh?.Invoke(false, null);
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
            if (createReturn.success)
            {
                room = createReturn.roomData;
                onRoomInfoRefresh?.Invoke(true, room);
            }
            else
            {
                onRoomInfoRefresh?.Invoke(false, null);
            }
        }
    }
}