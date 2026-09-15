using CrystalMagic.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Server
{
    public class ServerBattleManager : Singleton<ServerBattleManager>
    {
        public ServerService battleService;
        public ServerService lobbyService;
        public Dictionary<ulong, BattleRoom> battleRooms = new Dictionary<ulong, BattleRoom>();
        public Dictionary<Connect, BattlePlayer> connectDic = new Dictionary<Connect, BattlePlayer>();


        public Connect lobbyConnect;
        protected override void Awake()
        {
            base.Awake();
            TCPPacketCode.Init();
            battleService = new ServerService(ServerUtility.GetBattleIPEndPoint());
            battleService.OnAccept += OnAccept;
            battleService.OnDisconnected += OnDisconnected;
            battleService.Init();

            lobbyService = new ServerService(ServerUtility.GetBattleLobbyIPEndPoint());
            lobbyService.OnAccept += OnLobbyAccept;
            lobbyService.OnDisconnected += OnDisconnected;
            lobbyService.Init();
        }

        private void OnDisconnected(Connect connect)
        {
            if (connectDic.TryGetValue(connect, out BattlePlayer player))
            {
                connectDic.Remove(connect);
                player.connect = null;
                player.entered = false;
                player.characterData = null;
            }
        }

        private void OnAccept(Connect connect)
        {
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2B_EnterBattle>(), OnEnterBattle);
        }

        private void OnLobbyAccept(Connect connect)
        {
            lobbyConnect = connect;
            connect.RegisterCallback(TCPPacketCode.GetOpcode<L2B_StartRoom>(), OnStartRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<L2B_TryReloadRoom>(), OnReloadRoom);
        }

        private void OnReloadRoom(IMessage message, Connect connect)
        {
            
        }

        private void OnStartRoom(IMessage message, Connect connect)
        {
            L2B_StartRoom realMessage = message as L2B_StartRoom;
            if (realMessage == null)
            {
                return;
            }

            BattleRoom room = BattleRoom.CreateRoom(realMessage.roomId, realMessage.ownerAccountId, realMessage.dungeonFloor, realMessage.players);
            battleRooms.Add(room.battleId, room);

            Dictionary<ulong, string> keys = new Dictionary<ulong, string>();
            foreach(var player in room.players.Keys)
            {
                string key = ServerUtility.CreateBattleTicket();
                room.secretKeys.Add(key, room.players[player]);
                keys[player] = key;
            }
            lobbyConnect.Send(new B2L_StartRoomResult { secretKeys = keys, roomId = realMessage.roomId });
        }

        private void OnEnterBattle(IMessage message, Connect connect)
        {
            C2B_EnterBattle realMessage = message as C2B_EnterBattle;
            if (realMessage != null &&
                !string.IsNullOrEmpty(realMessage.ticket) &&
                realMessage.data != null)
            {
                foreach (BattleRoom room in battleRooms.Values)
                {
                    if (room.secretKeys.TryGetValue(realMessage.ticket, out BattlePlayer player))
                    {
                        if (player.entered || player.connect != null)
                        {
                            break;
                        }

                        room.secretKeys.Remove(realMessage.ticket);
                        player.connect = connect;
                        player.characterData = realMessage.data;
                        player.entered = true;
                        connectDic[connect] = player;
                        connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2B_EnterBattle>(), OnEnterBattle);
                        connect.Send(new B2C_EnterBattleResult()
                        {
                            type = BattleRequestType.EnterBattleSuccess,
                        });

                        foreach (BattlePlayer battlePlayer in room.players.Values)
                        {
                            if (!battlePlayer.entered)
                            {
                                return;
                            }
                        }

                        Debug.Log($"[Battle] All players entered battle {room.battleId}.");
                        return;
                    }
                }
            }

            connect.Send(new B2C_EnterBattleResult() { type = BattleRequestType.EnterBattleFail });
            battleService.DisconnectAfterSend(connect);
        }
        private void LateUpdate()
        {
            battleService.Update();
            lobbyService.Update();
        }
    }
}
