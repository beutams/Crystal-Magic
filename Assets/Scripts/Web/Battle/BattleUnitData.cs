using CrystalMagic.Core;
using System;

namespace Server
{
    [Serializable]
    public class BattleUnitData
    {
        public Guid unitId;
        public ulong accountId;
        public CharacterData characterData;
    }
}
