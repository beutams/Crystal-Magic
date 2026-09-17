using System;

namespace Server
{
    [Message(Opcode = 45)]
    [Serializable]
    public class C2B_BattleSceneReady : IMessage
    {
        public ulong battleId;
        public uint connectVersion;
    }
}
