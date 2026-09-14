using System;

namespace Server
{
    [Message(Opcode = 38)]
    [Serializable]
    public class B2C_CreateBattleUnit : IMessage
    {
        public BattleUnitData unitData;
    }
}
