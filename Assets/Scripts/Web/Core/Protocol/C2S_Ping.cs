using System;

namespace Server
{
    [Message(Opcode = 2)]
    [Serializable]
    public class C2S_Ping : IMessage
    {
        public long Time { get; set; }
    }
}
