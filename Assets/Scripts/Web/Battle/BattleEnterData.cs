using System;

namespace Server
{
    [Serializable]
    public class BattleEnterData
    {
        public ulong battleId;
        public int dungeonFloor;
        public int seed;
    }
}
