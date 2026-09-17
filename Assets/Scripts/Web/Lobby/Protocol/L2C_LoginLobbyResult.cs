using System;

namespace Server
{
    [Message(Opcode = 20)]
    [Serializable]
    public class L2C_LoginLobbyResult : IMessage
    {
        public LobbyRequestType type;
    }
}
