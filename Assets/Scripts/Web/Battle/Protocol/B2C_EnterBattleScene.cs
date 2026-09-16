using System;

namespace Server
{
    [Message(Opcode = 39)]
    [Serializable]
    public class B2C_EnterBattleScene : IMessage
    {
        public BattleEnterData battleData;
    }
}
