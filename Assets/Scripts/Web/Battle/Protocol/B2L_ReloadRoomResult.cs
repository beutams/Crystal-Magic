using System;

namespace Server
{
    [Message(Opcode = 37)]
    [Serializable]
    public class B2L_ReloadRoomResult : IMessage
    {
        public RoomData roomData { get; set; }
    }
}