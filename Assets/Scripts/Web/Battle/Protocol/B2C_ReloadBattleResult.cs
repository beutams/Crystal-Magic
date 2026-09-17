using System;

namespace Server
{
    [Message(Opcode = 44)]
    [Serializable]
    public class B2C_ReloadBattleResult : IMessage
    {
        public BattleRequestType type;
        public uint connectVersion;
        public bool isRunningReload;
    }
}
