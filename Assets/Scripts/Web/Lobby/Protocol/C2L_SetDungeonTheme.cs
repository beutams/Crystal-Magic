using System;

namespace Server
{
    [Message(Opcode = 16)]
    [Serializable]
    public class C2L_SetDungeonTheme : IMessage
    {
        public int themeKey;
    }
}
