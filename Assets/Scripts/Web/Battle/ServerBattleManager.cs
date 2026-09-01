using CrystalMagic.Core;
using System;
using System.Collections.Generic;

namespace Server
{
    public class ServerBattleManager : Singleton<ServerBattleManager>
    {
        public ServerService battleService;
        public Dictionary<ulong, BattleRoom> battleRooms = new Dictionary<ulong, BattleRoom>();
        public Dictionary<ulong, string> secretKeys = new Dictionary<ulong, string>();

        public Connect lobbyConnect;
        protected override void Awake()
        {
            base.Awake();
            battleService = new ServerService();
            battleService.OnAccept += OnAccept;
            battleService.OnDisconnected += OnDisconnected;

            battleService.Init();
        }

        private void OnDisconnected(Connect connect)
        {
            throw new NotImplementedException();
        }

        private void OnAccept(Connect connect)
        {
            if(connect.IPEndPoint == ServerUtility.GetLobbyIPEndPoint())
            {
                lobbyConnect = connect;
                lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2B_StartRoom>(), OnStartRoom);
                lobbyConnect.RegisterCallback(TCPPacketCode.GetOpcode<L2B_TryReloadRoom>(), OnReloadRoom);
                return;
            }
        }

        private void OnReloadRoom(IMessage message, Connect connect)
        {
            throw new NotImplementedException();
        }

        private void OnStartRoom(IMessage message, Connect connect)
        {
            L2B_StartRoom realMessage = message as L2B_StartRoom;
            BattleRoom room = BattleRoom.CreateRoom(realMessage.ownerAccountId,realMessage.players);
            battleRooms.Add(room.roomId, room);

            Dictionary<ulong, string> keys = new Dictionary<ulong, string>();
            foreach(var player in room.players.Keys)
            {
                string key = ServerUtility.CreateBattleTicket();
                secretKeys.Add(player, key);
                keys[player] = key;
            }
            lobbyConnect.Send(new B2L_StartRoomResult { secretKeys = keys, roomId = realMessage.roomId });
        }
    }
}
