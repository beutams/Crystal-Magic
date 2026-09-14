using System;

namespace Server
{
    [Message(Opcode = 16)]
    [Serializable]
    public class C2L_SetDungeonFloor : IMessage
    {
        public int dungeonFloor;
    }
}
