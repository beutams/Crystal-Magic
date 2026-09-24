using System;

namespace Server
{
    [Message(Opcode = 55)]
    [Serializable]
    public sealed class B2C_BeginBattleTheme : IMessage
    {
        public ulong battleId;
        public uint sceneVersion;
        public uint connectVersion;
    }
}
