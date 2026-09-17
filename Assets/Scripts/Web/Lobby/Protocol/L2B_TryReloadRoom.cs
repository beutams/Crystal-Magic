using System;

namespace Server
{
    [Message(Opcode = 31)]
    [Serializable]
    public class L2B_TryReloadRoom : IMessage
    {
        public ulong accountId { get; set; }
        public string saveGuid { get; set; }
    }
}
