using System;

namespace Server
{
    [Message(Opcode = 23)]
    [Serializable]
    public class L2C_JoinReturn : IMessage
    {
        public RoomData roomData { get; set; }
        public LobbyRequestType type {  get; set; }
    }

}