using System;

namespace Server
{
    [Message(Opcode = 56)]
    [Serializable]
    public sealed class B2C_BattleExitResult : IMessage
    {
        public ulong battleId;
        public uint sceneVersion;
        public bool accepted;
        public string error;
    }
}
