using System;
using System.Collections.Generic;

namespace Server
{
    [Message(Opcode = 47)]
    [Serializable]
    public class B2C_ReloadBattleSnapshot : IMessage
    {
        public ulong battleId;
        public uint connectVersion;
        public uint snapshotFrame;
        public NetworkEntitySpawnInfo[] entityInfos;
        public List<NetworkStateData> states;
    }
}
