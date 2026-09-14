using CrystalMagic.Core;
using System;

namespace Server
{
    [Message(Opcode = 35)]
    [Serializable]
    public class C2B_SendUserData : IMessage
    {
        public CharacterData data;
    }
}
