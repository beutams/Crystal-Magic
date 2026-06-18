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

        [EditorLabel("Layout Search Attempt Limit")]
        public int LayoutSearchAttemptLimit = 100;

        [EditorLabel("Use Best Candidate Fallback")]
        public bool UseBestCandidateFallback = true;

        [EditorLabel("Default Corridor Material")]
        public string DefaultCorridorMaterialPath = "Assets/Res/Material/TrainPlane.mat";

        [EditorLabel("Default Room Material")]
        public string DefaultRoomMaterialPath = "Assets/Res/Material/Plane.mat";

        [EditorLabel("Default Ante Room Material")]
        public string DefaultAnteRoomMaterialPath = "Assets/Res/Material/TrainPlane.mat";

        [EditorLabel("Default Wall Material")]
        public string DefaultWallMaterialPath = "Assets/Res/Material/NPC1.mat";

        [EditorLabel("Default Start Marker Material")]
        public string DefaultStartMarkerMaterialPath = "Assets/Res/Material/NPC2.mat";

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
