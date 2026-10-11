
using System;

namespace Server
{
    [Message(Opcode = 12)]
    [Serializable]
    public class C2L_CreateRoom : IMessage
    {
        public string roomName;
        public string displayName;
        public int maxPlayers = RoomCreationRules.MaxPlayers;
    }
}
