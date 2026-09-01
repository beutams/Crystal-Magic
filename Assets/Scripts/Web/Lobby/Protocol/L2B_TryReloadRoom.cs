using System;

namespace Server
{
    [Message(Opcode = 31)]
    [Serializable]
    public class L2B_TryReloadRoom : IMessage
    {
        public RoomData roomData { get; set; }
    }
}