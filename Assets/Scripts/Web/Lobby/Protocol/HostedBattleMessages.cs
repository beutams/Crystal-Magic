using System;

namespace Server
{
    [Serializable, Message(Opcode = 60)]
    public sealed class L2C_HostBattleStart : IMessage
    {
        public BattleConnectionInfo connection;
        public L2B_StartRoom room;
        public ulong[] steamMembers;
        public bool tcpRequired;
    }
    [Serializable, Message(Opcode = 61)]
    public sealed class C2L_HostBattleReady : IMessage { public B2L_StartRoomResult result; }
    [Serializable, Message(Opcode = 62)]
    public sealed class L2C_HostBattleCancel : IMessage { public string sessionId; public string error; }
    [Serializable, Message(Opcode = 63)]
    public sealed class C2L_HostBattleEnded : IMessage { public string sessionId; }
    [Serializable, Message(Opcode = 64)]
    public sealed class L2C_HostReloadRequest : IMessage
    {
        public string sessionId;
        public string requestId;
        public L2B_TryReloadRoom request;
    }
    [Serializable, Message(Opcode = 65)]
    public sealed class C2L_HostReloadResult : IMessage
    {
        public string sessionId;
        public string requestId;
        public B2L_ReloadRoomResult result;
    }
}
