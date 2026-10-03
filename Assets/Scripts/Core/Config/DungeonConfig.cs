using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using UnityEngine;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    [GameConfig]
    [EditorLabel("Dungeon Config")]
    public sealed class DungeonConfig
    {
        [EditorLabel("Map Width")]
        public int MapWidth = 200;

        [EditorLabel("Map Height")]
        public int MapHeight = 200;

        [EditorLabel("Initial Theme Id")]
        public int InitialThemeId;

        public const int LevelsPerTheme = 10;

        [EditorLabel("Chest Reward Count Range (Min / Max)")]
        public Vector2Int ChestRewardCountRange = new(3, 4);

        [EditorLabel("Copper Chest Counts (Small / Medium / Large Interest)")]
        public Vector3Int CopperChestCounts = new(1, 1, 1);

        [EditorLabel("Silver Chest Counts (Small / Medium / Large Interest)")]
        public Vector3Int SilverChestCounts = new(0, 1, 1);

        [EditorLabel("Gold Chest Counts (Small / Medium / Large Interest)")]
        public Vector3Int GoldChestCounts = new(0, 0, 1);

        [EditorLabel("Copper Chest Luck Range")]
        public Vector2 CopperChestLuckRange = new(0f, 0.4f);

        [EditorLabel("Silver Chest Luck Range")]
        public Vector2 SilverChestLuckRange = new(0.4f, 0.7f);

        [EditorLabel("Gold Chest Luck Range")]
        public Vector2 GoldChestLuckRange = new(0.7f, 1f);

        [EditorLabel("Rarity 0 Weight (Luck 0 / 1)")]
        public Vector2 ChestRarity0Weight = new(10f, 0f);

        [EditorLabel("Rarity 1 Weight (Luck 0 / 1)")]
        public Vector2 ChestRarity1Weight = new(8f, 0f);

        [EditorLabel("Rarity 2 Weight (Luck 0 / 1)")]
        public Vector2 ChestRarity2Weight = new(6f, 0f);

        [EditorLabel("Rarity 3 Weight (Luck 0 / 1)")]
        public Vector2 ChestRarity3Weight = new(4f, 5f);

        [EditorLabel("Rarity 4 Weight (Luck 0 / 1)")]
        public Vector2 ChestRarity4Weight = new(2f, 4f);

        [EditorLabel("Rarity 5 Weight (Luck 0 / 1)")]
        public Vector2 ChestRarity5Weight = new(1f, 1f);

        public void EnsureValid()
        {
            MapWidth = Mathf.Max(8, MapWidth);
            MapHeight = Mathf.Max(8, MapHeight);
            InitialThemeId = Mathf.Max(0, InitialThemeId);
            ChestRewardCountRange.x = Mathf.Max(1, ChestRewardCountRange.x);
            ChestRewardCountRange.y = Mathf.Max(ChestRewardCountRange.x, ChestRewardCountRange.y);
            CopperChestCounts = Vector3Int.Max(Vector3Int.zero, CopperChestCounts);
            SilverChestCounts = Vector3Int.Max(Vector3Int.zero, SilverChestCounts);
            GoldChestCounts = Vector3Int.Max(Vector3Int.zero, GoldChestCounts);
            CopperChestLuckRange = ClampLuckRange(CopperChestLuckRange);
            SilverChestLuckRange = ClampLuckRange(SilverChestLuckRange);
            GoldChestLuckRange = ClampLuckRange(GoldChestLuckRange);
            ChestRarity0Weight = ClampWeights(ChestRarity0Weight);
            ChestRarity1Weight = ClampWeights(ChestRarity1Weight);
            ChestRarity2Weight = ClampWeights(ChestRarity2Weight);
            ChestRarity3Weight = ClampWeights(ChestRarity3Weight);
            ChestRarity4Weight = ClampWeights(ChestRarity4Weight);
            ChestRarity5Weight = ClampWeights(ChestRarity5Weight);
        }

        public int GetChestCount(OpenFieldInterestSize interestSize, DungeonTreasureQuality quality)
        {
            Vector3Int counts = quality switch
            {
                DungeonTreasureQuality.Copper => CopperChestCounts,
                DungeonTreasureQuality.Silver => SilverChestCounts,
                DungeonTreasureQuality.Gold => GoldChestCounts,
                _ => Vector3Int.zero,
            };

            return interestSize switch
            {
                OpenFieldInterestSize.Small => counts.x,
                OpenFieldInterestSize.Medium => counts.y,
                _ => counts.z,
            };
        }

        public Vector2 GetChestLuckRange(DungeonTreasureQuality quality)
        {
            return quality switch
            {
                DungeonTreasureQuality.Copper => CopperChestLuckRange,
                DungeonTreasureQuality.Silver => SilverChestLuckRange,
                _ => GoldChestLuckRange,
            };
        }

        public float GetChestRarityWeight(int rarity, float luck)
        {
            Vector2 weights = Mathf.Clamp(rarity, 0, 5) switch
            {
                0 => ChestRarity0Weight,
                1 => ChestRarity1Weight,
                2 => ChestRarity2Weight,
                3 => ChestRarity3Weight,
                4 => ChestRarity4Weight,
                _ => ChestRarity5Weight,
            };
            return Mathf.Lerp(Mathf.Max(0f, weights.x), Mathf.Max(0f, weights.y), Mathf.Clamp01(luck));
        }

        private static Vector2 ClampLuckRange(Vector2 range)
        {
            range.x = Mathf.Clamp01(range.x);
            range.y = Mathf.Clamp(range.y, range.x, 1f);
            return range;
        }

        private static Vector2 ClampWeights(Vector2 weights)
        {
            return new Vector2(Mathf.Max(0f, weights.x), Mathf.Max(0f, weights.y));
        }
    }
}
