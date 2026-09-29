using System;

namespace Server
{
    [Message(Opcode = 10)]
    [Serializable]
    public class C2L_LoginLobby : IMessage
    {
        public ulong accountId;
        public string username;
        public string saveGuid;
        public bool steamP2PAvailable;
        public int protocolVersion;
        public string activeSessionId;
    }
}
