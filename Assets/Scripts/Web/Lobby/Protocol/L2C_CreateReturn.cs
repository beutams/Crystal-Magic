using System;

namespace Server
{
    [Message(Opcode = 22)]
    [Serializable]
    public class L2C_CreateReturn : IMessage
    {
        public bool success;
        public RoomData roomData { get; set; }
    }
}