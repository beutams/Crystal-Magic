using System;
using CrystalMagic.Core;

namespace Server
{
    [Message(Opcode = 43)]
    [Serializable]
    public class C2B_ReloadBattle : IMessage
    {
        public string ticket;
        public string saveGuid;
        public CharacterData data;
    }
}
