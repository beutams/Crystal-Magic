using CrystalMagic.Core;
using System;
using System.Collections.Generic;

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
        public string sessionId;
        public uint connectVersion;
        public uint sceneVersion;
        public BattleSettlementOutcome outcome;
        public CharacterData characterData;
        public List<InventoryItemData> acquiredItems;
        public long acquiredMoney;
        public int reachedFloor;
    }

    [Message(Opcode = 57), Serializable]
    public sealed class C2B_BattleSettlementAck : IMessage
    {
        public ulong battleId;
        public string sessionId;
        public uint connectVersion;
    }
}
