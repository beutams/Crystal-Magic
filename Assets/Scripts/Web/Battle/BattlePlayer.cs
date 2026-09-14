using CrystalMagic.Core;
using System;
using Unity.Entities;

namespace Server
{
    public class BattlePlayer
    {
        public ulong accountId;
        public Connect connect;

        public BattleRoom room;

        public bool init;
        public Guid unitId;
        public Entity entity;
        public CharacterData characterData;
    }
}
