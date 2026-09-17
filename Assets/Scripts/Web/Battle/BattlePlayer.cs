using CrystalMagic.Core;
using System;
using Unity.Entities;
using UnityEngine;

namespace Server
{
    public class BattlePlayer
    {
        public ulong accountId;
        public string saveGuid;
        public Connect connect;

        public BattleRoom room;

        public bool entered;
        public bool sceneReady;
        public bool entitiesSent;
        public bool ready;
        public bool offline;
        public bool active;
        public bool runningReload;
        public bool reloadSnapshotRequested;
        public bool reloadSnapshotSent;
        public uint connectVersion;
        public uint reloadVersion;
        public uint snapshotFrame;
        public long reloadTimerId;
        public Guid unitId;
        public Entity entity;
        public Vector3 spawnWorldPosition;
        public CharacterData characterData;
    }
}
