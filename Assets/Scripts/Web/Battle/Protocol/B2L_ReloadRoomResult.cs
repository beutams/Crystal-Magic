using System;

namespace Server
{
    public enum BattleReloadRoomResultType
    {
        NoBattle = 0,
        ReloadAvailable = 1,
        SaveMismatch = 2,
    }

    [Message(Opcode = 37)]
    [Serializable]
    public class B2L_ReloadRoomResult : IMessage
    {
        public ulong accountId { get; set; }
        public string saveGuid { get; set; }
        public BattleReloadRoomResultType type { get; set; }
        public string ticket { get; set; }
    }
}
