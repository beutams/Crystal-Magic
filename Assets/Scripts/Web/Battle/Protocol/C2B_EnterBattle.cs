using System;
using CrystalMagic.Core;

namespace Server
{
    [Message(Opcode = 33)]
    [Serializable]
    public class C2B_EnterBattle : IMessage
    {
        public string ticket;
        public CharacterData data;
    }
}
