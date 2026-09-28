using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalMagic.Core
{
    /// <summary>
    /// Mountain sprites baked from the approved sample. Each of eight neighbours
    /// has three states: outside, cliff, summit. A direct lookup handles both
    /// the outer silhouette and internal cliff contacts without rule-order ambiguity.
    /// </summary>
    [CreateAssetMenu(menuName = "2D/Tiles/Mountain Rule Tile")]
    public sealed class MountainRuleTile : RuleTile
    {
        public enum MountainPart { Foot, Wall, Summit }
        public const int NeighbourCaseCount = 6561;
        private static readonly Vector3Int[] Directions =
        {
            new(0, 1, 0), new(1, 1, 0), new(1, 0, 0), new(1, -1, 0),
            new(0, -1, 0), new(-1, -1, 0), new(-1, 0, 0), new(-1, 1, 0),
        };

        public string Family = "PrairieMountain";
        public MountainPart Part;
        [Tooltip("Deduplicated 16x16 sprites generated from the approved mountain sample.")]
        public Sprite[] VariantSprites = Array.Empty<Sprite>();
        [HideInInspector] public int[] VariantLookup = Array.Empty<int>();

        public int GetNeighbourState(TileBase other)
        {
            if (other is RuleOverrideTile ruleOverride)
                other = ruleOverride.m_InstanceTile != null ? ruleOverride.m_InstanceTile : ruleOverride.m_Tile;

            if (other is not MountainRuleTile mountain ||
                (other != this && (string.IsNullOrEmpty(Family) ||
                 !string.Equals(Family, mountain.Family, StringComparison.Ordinal))))
                return 0;
            return mountain.Part == MountainPart.Summit ? 2 : 1;
        }

        public Sprite GetSpriteForNeighbourKey(int key)
        {
            if (VariantLookup == null || VariantSprites == null ||
                VariantLookup.Length != NeighbourCaseCount || key < 0 || key >= NeighbourCaseCount)
                return m_DefaultSprite;
            int index = VariantLookup[key];
            return index >= 0 && index < VariantSprites.Length && VariantSprites[index] != null
                ? VariantSprites[index] : m_DefaultSprite;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            int key = 0, multiplier = 1;
            foreach (Vector3Int direction in Directions)
            {
                key += GetNeighbourState(tilemap.GetTile(position + direction)) * multiplier;
                multiplier *= 3;
            }
            tileData.sprite = GetSpriteForNeighbourKey(key);
            tileData.color = Color.white;
            tileData.gameObject = m_DefaultGameObject;
            tileData.colliderType = m_DefaultColliderType;
            tileData.flags = TileFlags.LockTransform;
            tileData.transform = Matrix4x4.identity;
        }

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            tilemap.RefreshTile(position);
            foreach (Vector3Int direction in Directions)
                tilemap.RefreshTile(position + direction);
        }

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            bool connected = GetNeighbourState(other) != 0;
            return neighbor switch
            {
                TilingRule.Neighbor.This => connected,
                TilingRule.Neighbor.NotThis => !connected,
                _ => base.RuleMatch(neighbor, other),
            };
        }
    }
}
