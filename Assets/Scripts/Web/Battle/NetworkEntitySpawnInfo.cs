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
        public bool hasScale;
        public float scaleX;
        public float scaleY;
        public float scaleZ;
        public bool hasCollider;
        public bool colliderEnabled;
        public float colliderSizeX;
        public float colliderSizeY;
        public float colliderSizeZ;
        public float health;
        public float mana;
        public bool hasHealth;
        public bool hasMana;
        public CharacterData characterData;
        public ulong characterRevision;
        public bool hasBattlePlayerStatus;
        public BattlePlayerLifeState battlePlayerLifeState;
        public BattlePlayerConnectionState battlePlayerConnectionState;
        public bool battlePlayerTransitionReady;
        public bool hasFaction;
        public UnitFactionType faction;
        public bool hasInteractableData;
        public InteractionKind interactionKind;
        public int interactionDataId;
        public int interactionAmount;
        public int interactionVariant;
        public float interactionRangeSq;
        public bool interactionEnabled;
        public bool hasDropScatter;
        public float dropScatterStartX;
        public float dropScatterStartY;
        public float dropScatterStartZ;
        public float dropScatterTargetX;
        public float dropScatterTargetY;
        public float dropScatterTargetZ;
        public float dropScatterDurationSeconds;
        public float dropScatterElapsedSeconds;
        public float dropScatterArcHeight;
        public bool dropScatterLanded;
        public bool hasMonsterSpawnData;
        public int monsterSaveId;
        public int monsterRegionId;
        public int monsterSquadId;
        public bool monsterIsBoss;
        public bool hasTreasureData;
        public int treasureRegionId;
        public uint treasureRandomSeed;
        public byte treasureInterestSize;
        public bool treasureIsOpened;
        public int[] treasureCandidateItemIds;
    }
}
