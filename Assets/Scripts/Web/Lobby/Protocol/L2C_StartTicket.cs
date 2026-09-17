
using System;

namespace Server
{
    [Message(Opcode = 32)]
    [Serializable]
    public class L2C_StartTicket : IMessage
    {
        public string ticket;
        public bool reload;
    }
}
