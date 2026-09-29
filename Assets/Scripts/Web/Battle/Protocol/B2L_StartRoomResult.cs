using System;
using System.Collections.Generic;

namespace Server
{
    [Message(Opcode = 36)]
    [Serializable]
    public class B2L_StartRoomResult : IMessage
    {
        public ulong roomId;
        public BattleConnectionInfo connection;
        public string error;
        public Dictionary<ulong, string> secretKeys;
    }
}
