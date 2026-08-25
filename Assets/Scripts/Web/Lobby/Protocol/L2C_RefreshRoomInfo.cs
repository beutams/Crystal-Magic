using System;

namespace Server
{
    [Message(Opcode = 25)]
    [Serializable]
    public class L2C_RefreshRoomInfo : IMessage
    {
        public RoomData roomData { get; set; }
    }
}