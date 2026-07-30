using System;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("Dungeon Config")]
    public sealed class DungeonConfig
    {
        [EditorLabel("Boss Floor Interval")]
        public int BossFloorInterval = 10;

        [EditorLabel("Theme Band Size")]
        public int ThemeBandSize = 10;

        [EditorLabel("Cell World Size")]
        public float CellWorldSize = 2f;

        [EditorLabel("Fallback Mob1 Pool Id")]
        public int FallbackMob1PoolId = 1;

        [EditorLabel("Fallback Mob2 Pool Id")]
        public int FallbackMob2PoolId = 2;

        [EditorLabel("Fallback Mob3 Pool Id")]
        public int FallbackMob3PoolId = 3;

        [EditorLabel("Fallback Treasure1 Pool Id")]
        public int FallbackTreasure1PoolId = -1;

        [EditorLabel("Fallback Treasure2 Pool Id")]
        public int FallbackTreasure2PoolId = -1;

        [EditorLabel("Fallback Treasure3 Pool Id")]
        public int FallbackTreasure3PoolId = -1;

        [EditorLabel("Fallback Boss Room Id")]
        public int FallbackBossRoomId = 1;
    }
}
