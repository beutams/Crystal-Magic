
using System;

namespace Server
{
    [Message(Opcode = 15)]
    [Serializable]
    public class C2L_Start : IMessage
    {

    }
}
