using System;
using System.Collections.Generic;

namespace Server
{
    [Message(Opcode = 30)]
    [Serializable]
    public class L2B_StartRoom : IMessage
    {
        public ulong roomId;
        public ulong ownerAccountId;
        public int themeKey;
        public ulong[] players;
        public Dictionary<ulong, string> saveGuids;
    }
}
