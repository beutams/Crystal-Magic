using System;

namespace Server
{
    [Message(Opcode = 21)]
    [Serializable]
    public class L2C_RefreshRoomList : IMessage
    {
        public RoomListData roomListData { get; set; }
    }
}