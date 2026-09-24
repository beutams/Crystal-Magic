using System;

namespace Server
{
    [Serializable]
    public class BattleEnterData
    {
        public ulong battleId;
        public uint sceneVersion;
        public int themeKey;
        public int seed;
    }
}
