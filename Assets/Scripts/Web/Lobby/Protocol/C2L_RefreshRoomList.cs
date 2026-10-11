using System;

namespace Server
{
    [Serializable, Message(Opcode = 17)]
    public sealed class C2L_RefreshRoomList : IMessage { }
}
