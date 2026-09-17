using System;

namespace Server
{
    [Message(Opcode = 26)]
    [Serializable]
    public class L2C_StartReturn : IMessage
    {
        public LobbyRequestType type;
    }
}
