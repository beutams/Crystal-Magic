using System;
using System.Collections.Generic;

namespace Server
{
    public class BattleRoom
    {
        public ulong battleId;
        public ulong lobbyRoomId;
        public ulong ownerAccountId;
        public int dungeonFloor;
        public int seed;
        public BattlePhase phase;
        public Dictionary<ulong, BattlePlayer> players;
        public Dictionary<string, BattlePlayer> secretKeys;
        public static BattleRoom CreateRoom(ulong lobbyRoomId, ulong ownerAccountId, int dungeonFloor, ulong[] playerlist)
        {
            BattleRoom room = new BattleRoom();
            room.battleId = ServerUtility.CreateBattleRoomId();
            room.lobbyRoomId = lobbyRoomId;
            room.ownerAccountId = ownerAccountId;
            room.dungeonFloor = Math.Max(1, dungeonFloor);
            room.seed = ServerUtility.CreateBattleSeed();
            room.players = new Dictionary<ulong, BattlePlayer>();
            room.secretKeys = new Dictionary<string, BattlePlayer>();
            foreach(var accountId in playerlist)
            {
                room.players.Add(accountId, new BattlePlayer()
                {
                    accountId = accountId,
                    room = room,
                });
            }

            room.phase = BattlePhase.WaitingForEnter;
            return room;
        }
    }
}
