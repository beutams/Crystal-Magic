using System;
using CrystalMagic.Core;

namespace Server
{
    public enum NetworkEntityPrefabType
    {
        Unit = 0,
        Environment = 1,
        Drop = 2,
        Projectile = 3,
    }

    [Serializable]
    public class NetworkEntitySpawnInfo
    {
        public Guid unitId;
        public NetworkEntityPrefabType prefabType;
        public string prefabName;
        public ulong ownerAccountId;
        public float x;
        public float y;
        public float z;
        public float health;
        public float mana;
        public CharacterData characterData;
    }
}
