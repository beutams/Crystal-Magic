using System;

namespace Server
{
    [Message(Opcode = 42)]
    [Serializable]
    public class B2C_StartFrame : IMessage
    {
        public ulong battleId;
    }
}
