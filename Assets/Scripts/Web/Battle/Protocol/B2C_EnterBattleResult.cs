using System;

namespace Server
{
    [Message(Opcode = 34)]
    [Serializable]
    public class B2C_EnterBattleResult : IMessage
    {
        public BattleRequestType type;
    }
}
