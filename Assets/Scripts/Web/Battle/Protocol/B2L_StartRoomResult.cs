using System;
using System.Collections.Generic;

namespace Server
{
    [Message(Opcode = 36)]
    [Serializable]
    public class B2L_StartRoomResult : IMessage
    {
        public ulong roomId;
        public Dictionary<ulong, string> secretKeys;
    }
}