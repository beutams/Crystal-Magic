
using System;

namespace Server
{
    [Message(Opcode = 13)]
    [Serializable]
    public class C2L_JoinRoom : IMessage
    {
        public long roomId;

    }
}
