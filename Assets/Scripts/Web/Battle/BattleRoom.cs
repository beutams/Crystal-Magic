using System.Collections.Generic;

namespace Server
{
    public class BattleRoom
    {
        public ulong roomId;
        public ulong ownerAccountId;
        public Dictionary<ulong, BattlePlayer> players;
        public Dictionary<string, BattlePlayer> secretKeys;

        public static BattleRoom CreateRoom(ulong ownerAccountId,ulong[] playerlist)
        {
            BattleRoom room = new BattleRoom();
            room.roomId = ServerUtility.CreateBattleRoomId();
            room.ownerAccountId = ownerAccountId;
            room.players = new Dictionary<ulong, BattlePlayer>();
            room.secretKeys = new Dictionary<string, BattlePlayer>();
            foreach(var player in playerlist)
            {
                room.players.Add(player, new BattlePlayer() { accountId = player });
            }
            return room;
        }
    }
}
