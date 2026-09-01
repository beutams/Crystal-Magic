using System;

namespace Server
{
    [Message(Opcode = 30)]
    [Serializable]
    public class L2B_StartRoom : IMessage
    {
        public ulong roomId;
        public ulong ownerAccountId;
        public ulong[] players;
    }
}