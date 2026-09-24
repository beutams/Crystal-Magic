using System;

namespace Server
{
    public enum BattleExitRequestType : byte
    {
        NextTheme = 0,
        Retreat = 1,
    }

    [Message(Opcode = 53)]
    [Serializable]
    public sealed class C2B_BattleExitRequest : IMessage
    {
        public ulong battleId;
        public uint connectVersion;
        public uint sceneVersion;
        public BattleExitRequestType type;
        public Guid exitUnitId;
    }
}
