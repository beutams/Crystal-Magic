
using System;

namespace Server
{
    [Message(Opcode = 24)]
    [Serializable]
    public class L2C_LeaveReturn : IMessage
    {
        public bool success;
    }
}
