using CrystalMagic.Core;
using System;

namespace Server
{
    public enum BattleSettlementOutcome : byte
    {
        Escaped = 0,
        Defeated = 1,
    }

    [Message(Opcode = 54)]
    [Serializable]
    public sealed class B2C_BattleSettlement : IMessage
    {
        public ulong battleId;
        public BattleSettlementOutcome outcome;
        public CharacterData characterData;
    }
}
