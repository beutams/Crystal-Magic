using System;

namespace Server
{
    [Message(Opcode = 1)]
    [Serializable]
    public class S2C_Pong : IMessage
    {
        public long Time { get; set; }
    }
}
