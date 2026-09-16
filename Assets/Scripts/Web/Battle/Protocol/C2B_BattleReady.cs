using System;

namespace Server
{
    [Message(Opcode = 41)]
    [Serializable]
    public class C2B_BattleReady : IMessage
    {
        public ulong battleId;
    }
}
