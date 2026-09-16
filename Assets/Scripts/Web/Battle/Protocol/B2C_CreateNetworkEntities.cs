using System;

namespace Server
{
    [Message(Opcode = 40)]
    [Serializable]
    public class B2C_CreateNetworkEntities : IMessage
    {
        public ulong battleId;
        public NetworkEntitySpawnInfo[] entityInfos;
    }
}
