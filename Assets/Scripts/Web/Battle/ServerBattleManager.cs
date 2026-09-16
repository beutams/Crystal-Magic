using CrystalMagic.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Server
{
    public class ServerBattleManager : Singleton<ServerBattleManager>
    {
        public ServerService battleService;
        public ServerService lobbyService;
        public ServerFrameManager frame;
        public Dictionary<ulong, BattleRoom> battleRooms = new Dictionary<ulong, BattleRoom>();
        public Dictionary<Connect, BattlePlayer> connectDic = new Dictionary<Connect, BattlePlayer>();

        public Connect lobbyConnect;
        protected override void Awake()
        {
            base.Awake();
            TCPPacketCode.Init();
            frame = ServerFrameManager.Instance;
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
            frame.RemoveConnect(connect);
            if (connectDic.TryGetValue(connect, out BattlePlayer player))
            {
                connectDic.Remove(connect);
                player.connect = null;
            }
        }

        private void OnAccept(Connect connect)
        {
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2B_EnterBattle>(), OnEnterBattle);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<C2B_BattleReady>(), OnBattleReady);
        }

        private void OnLobbyAccept(Connect connect)
        {
            lobbyConnect = connect;
            connect.RegisterCallback(TCPPacketCode.GetOpcode<L2B_StartRoom>(), OnStartRoom);
            connect.RegisterCallback(TCPPacketCode.GetOpcode<L2B_TryReloadRoom>(), OnReloadRoom);
        }

        private void OnReloadRoom(IMessage message, Connect connect)
        {
            L2B_TryReloadRoom realMessage = message as L2B_TryReloadRoom;
            if (realMessage == null || realMessage.accountId == 0UL)
            {
                return;
            }

            foreach (BattleRoom room in battleRooms.Values)
            {
                if (room.phase == BattlePhase.Finished ||
                    !room.players.TryGetValue(realMessage.accountId, out BattlePlayer player) ||
                    !player.entered ||
                    player.connect != null)
                {
                    continue;
                }

                foreach (string key in room.secretKeys
                             .Where(pair => pair.Value == player)
                             .Select(pair => pair.Key)
                             .ToArray())
                {
                    room.secretKeys.Remove(key);
                }

                string ticket = ServerUtility.CreateBattleTicket();
                room.secretKeys.Add(ticket, player);
                connect.Send(new B2L_ReloadRoomResult
                {
                    accountId = realMessage.accountId,
                    ticket = ticket,
                });
                return;
            }
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
            if (realMessage != null && !string.IsNullOrEmpty(realMessage.ticket))
            {
                foreach (BattleRoom room in battleRooms.Values)
                {
                    if (room.secretKeys.TryGetValue(realMessage.ticket, out BattlePlayer player))
                    {
                        if (player.connect != null || (!player.entered && realMessage.data == null))
                        {
                            break;
                        }

                        room.secretKeys.Remove(realMessage.ticket);
                        player.connect = connect;
                        connectDic[connect] = player;
                        connect.UnRegisterCallback(TCPPacketCode.GetOpcode<C2B_EnterBattle>(), OnEnterBattle);
                        connect.Send(new B2C_EnterBattleResult()
                        {
                            type = BattleRequestType.EnterBattleSuccess,
                        });

                        if (player.entered)
                        {
                            Debug.Log($"[Battle] Player {player.accountId} reconnected to battle {room.battleId}.");
                            return;
                        }

                        player.characterData = realMessage.data;
                        player.entered = true;

                        foreach (BattlePlayer battlePlayer in room.players.Values)
                        {
                            if (!battlePlayer.entered)
                            {
                                return;
                            }
                        }

                        Debug.Log($"[Battle] All players entered battle {room.battleId}.");
                        room.phase = BattlePhase.Initializing;
                        BattleEnterData battleData = new BattleEnterData
                        {
                            battleId = room.battleId,
                            dungeonFloor = room.dungeonFloor,
                            seed = room.seed,
                        };

                        foreach (BattlePlayer battlePlayer in room.players.Values)
                        {
                            battlePlayer.connect.Send(new B2C_EnterBattleScene
                            {
                                battleData = battleData,
                            });
                        }

                        GameWorldManager.CreateGameWorld(GameWorldRole.Server);
                        GameWorldManager.SetSceneMode(GameSceneMode.Dungeon);
                        room.phase = BattlePhase.WaitingForClientReady;
                        return;
                    }
                }
            }

            connect.Send(new B2C_EnterBattleResult() { type = BattleRequestType.EnterBattleFail });
            battleService.DisconnectAfterSend(connect);
        }

        private void OnBattleReady(IMessage message, Connect connect)
        {
            C2B_BattleReady realMessage = message as C2B_BattleReady;
            if (realMessage == null ||
                !connectDic.TryGetValue(connect, out BattlePlayer player) ||
                player.room.phase != BattlePhase.WaitingForClientReady ||
                player.room.battleId != realMessage.battleId ||
                player.ready)
            {
                return;
            }

            player.ready = true;
            foreach (BattlePlayer battlePlayer in player.room.players.Values)
            {
                if (!battlePlayer.ready)
                    return;
            }

            Debug.Log($"[Battle] All players initialized battle {player.room.battleId}.");
            foreach (BattlePlayer battlePlayer in player.room.players.Values)
            {
                frame.AddConnect(battlePlayer.connect);
            }

            player.room.phase = BattlePhase.Running;
            foreach (BattlePlayer battlePlayer in player.room.players.Values)
            {
                battlePlayer.connect.Send(new B2C_StartFrame { battleId = player.room.battleId });
            }

            frame.Start();
        }
        private void LateUpdate()
        {
            battleService.Update();
            lobbyService.Update();
            NetworkTimer.Instance.Update();
        }
    }
}
