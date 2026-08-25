using System;

namespace Server
{
    [Message(Opcode = 23)]
    [Serializable]
    public class L2C_JoinReturn : IMessage
    {
        public bool success;
        public RoomData roomData { get; set; }
    }
}