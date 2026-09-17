using System;
using System.Collections.Generic;

namespace Server
{
    public class BattleRoom
    {
        public ulong battleId;
        public ulong lobbyRoomId;
        public ulong ownerAccountId;
        public int themeKey;
        public int seed;
        public BattlePhase phase;
        public long phaseTimerId;
        public uint phaseVersion;
        public BattleEnterData battleData;
        public NetworkEntitySpawnInfo[] entityInfos;
        public ServerFrameManager frame;
        public BattleWorldContext world;
        public Dictionary<ulong, BattlePlayer> players;
        public Dictionary<string, BattlePlayer> secretKeys;
        public static BattleRoom CreateRoom(
            ulong lobbyRoomId,
            ulong ownerAccountId,
            int themeKey,
            ulong[] playerlist,
            Dictionary<ulong, string> saveGuids)
        {
            if (playerlist == null || saveGuids == null)
                return null;

            BattleRoom room = new BattleRoom();
            room.battleId = ServerUtility.CreateBattleRoomId();
            room.lobbyRoomId = lobbyRoomId;
            room.ownerAccountId = ownerAccountId;
            room.themeKey = Math.Max(0, themeKey);
            room.seed = ServerUtility.CreateBattleSeed();
            room.players = new Dictionary<ulong, BattlePlayer>();
            room.secretKeys = new Dictionary<string, BattlePlayer>();
            room.entityInfos = Array.Empty<NetworkEntitySpawnInfo>();
            room.frame = new ServerFrameManager();
            foreach(var accountId in playerlist)
            {
                if (!saveGuids.TryGetValue(accountId, out string saveGuid) ||
                    !Guid.TryParse(saveGuid, out Guid parsedSaveGuid))
                {
                    return null;
                }

                room.players.Add(accountId, new BattlePlayer()
                {
                    accountId = accountId,
                    saveGuid = parsedSaveGuid.ToString("N"),
                    room = room,
                });
            }

            room.phase = BattlePhase.WaitingForEnter;
            return room;
        }
    }
}
