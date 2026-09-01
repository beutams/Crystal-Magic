
using System;

namespace Server
{
    [Message(Opcode = 14)]
    [Serializable]
    public class C2L_Ready : IMessage
    {
        public bool ready;
    }
}
