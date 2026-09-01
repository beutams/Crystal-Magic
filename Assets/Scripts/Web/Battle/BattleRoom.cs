using System.Collections.Generic;

namespace Server
{
    public class BattleRoom
    {
        public ulong roomId;
        public ulong ownerAccountId;
        public Dictionary<ulong, BattlePlayer> players;
        public Dictionary<ulong, ulong> secretKeys;

        public static BattleRoom CreateRoom(ulong ownerAccountId,ulong[] playerlist)
        {
            BattleRoom room = new BattleRoom();
            room.roomId = ServerUtility.CreateBattleRoomId();
            room.ownerAccountId = ownerAccountId;
            room.players = new Dictionary<ulong, BattlePlayer>();
            foreach(var player in playerlist)
            {
                room.players.Add(player, new BattlePlayer());
            }
            return room;
        }
    }
}