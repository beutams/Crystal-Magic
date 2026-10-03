using System;
using System.Collections.Generic;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.OpenField
{
    public enum OpenFieldContentType : byte { Chest, InterestSquad, WildSquad }

    public sealed class OpenFieldContentPlacement
    {
        internal OpenFieldContentPlacement(
            OpenFieldContentType type,
            OpenFieldGridPosition cell,
            int encounterId,
            int squadId,
            DungeonTreasureQuality treasureQuality)
        {
            Type = type;
            Cell = cell;
            EncounterId = encounterId;
            SquadId = squadId;
            TreasureQuality = treasureQuality;
        }
        public OpenFieldContentType Type { get; }
        public OpenFieldGridPosition Cell { get; }
        public int EncounterId { get; }
        public int SquadId { get; }
        public DungeonTreasureQuality TreasureQuality { get; }
    }

    [Serializable]
    public sealed class OpenFieldDungeonContentConfig
    {
        public int WildSquadCount = 22;
        // Percentage points; 100 fills the floor's threat meter once.
        public Vector3 InterestClearThreat = new(8f, 13f, 20f);
        public float WildSquadClearThreat = 3f;
        public float PatrolReturnThreat = 40f;
        public int PlacementAttempts = 512;
        internal void EnsureValid()
        {
            WildSquadCount = Mathf.Max(0, WildSquadCount);
            InterestClearThreat = Vector3.Max(Vector3.zero, InterestClearThreat);
            WildSquadClearThreat = Mathf.Max(0f, WildSquadClearThreat);
            PatrolReturnThreat = Mathf.Max(0f, PatrolReturnThreat);
            PlacementAttempts = Mathf.Max(1, PlacementAttempts);
        }
        internal float GetClearThreat(OpenFieldInterestSize size) => size switch
        {
            OpenFieldInterestSize.Small => InterestClearThreat.x,
            OpenFieldInterestSize.Medium => InterestClearThreat.y,
            _ => InterestClearThreat.z,
        };
    }

    public static class OpenFieldDungeonContentGenerator
    {
        public static bool TryPlace(
            OpenFieldDungeonLayout layout,
            int seed,
            OpenFieldDungeonContentConfig config,
            DungeonConfig dungeonConfig)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (config == null) throw new ArgumentNullException(nameof(config));
            dungeonConfig ??= new DungeonConfig();
            config.EnsureValid();
            dungeonConfig.EnsureValid();
            layout.ClearContent();
            System.Random random = new(seed ^ 0x2D9973A1);
            int squadId = 1;
            foreach (OpenFieldInterestPoint point in layout.InterestPoints)
            {
                if (!TryPlaceInsidePoint(
                        layout,
                        random,
                        point,
                        OpenFieldContentType.InterestSquad,
                        point.EncounterId,
                        squadId++,
                        config.PlacementAttempts))
                {
                    layout.ClearContent();
                    return false;
                }

                for (int qualityIndex = (int)DungeonTreasureQuality.Copper;
                     qualityIndex <= (int)DungeonTreasureQuality.Gold;
                     qualityIndex++)
                {
                    DungeonTreasureQuality quality = (DungeonTreasureQuality)qualityIndex;
                    int chestCount = dungeonConfig.GetChestCount(point.Size, quality);
                    for (int i = 0; i < chestCount; i++)
                    {
                        if (!TryPlaceInsidePoint(
                                layout,
                                random,
                                point,
                                OpenFieldContentType.Chest,
                                point.EncounterId,
                                0,
                                config.PlacementAttempts,
                                quality))
                        {
                            layout.ClearContent();
                            return false;
                        }
                    }
                }
            }

            for (int i = 0; i < config.WildSquadCount; i++)
            {
                if (TryPlaceWild(layout, random, squadId++, config.PlacementAttempts))
                    continue;

                layout.ClearContent();
                return false;
            }

            return true;
        }

        private static bool TryPlaceInsidePoint(
            OpenFieldDungeonLayout l,
            System.Random r,
            OpenFieldInterestPoint p,
            OpenFieldContentType type,
            int encounterId,
            int squadId,
            int attempts,
            DungeonTreasureQuality treasureQuality = DungeonTreasureQuality.Copper)
        {
            int radius = Mathf.Max(1, p.Radius - 1), squared = radius * radius;
            for (int i = 0; i < attempts; i++)
            {
                OpenFieldGridPosition cell = new(r.Next(p.Center.X - radius, p.Center.X + radius + 1), r.Next(p.Center.Y - radius, p.Center.Y + radius + 1));
                int x = cell.X - p.Center.X, y = cell.Y - p.Center.Y;
                if (x * x + y * y > squared || !IsAvailable(l, cell))
                    continue;

                l.AddContent(type, cell, encounterId, squadId, treasureQuality);
                return true;
            }

            return false;
        }

        private static bool TryPlaceWild(OpenFieldDungeonLayout l, System.Random r, int squadId, int attempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                OpenFieldGridPosition cell = new(r.Next(0, l.Width), r.Next(0, l.Height));
                if (!l.IsReachable(cell.X, cell.Y) || IsInsideInterestPoint(l, cell) || !IsAvailable(l, cell))
                    continue;

                l.AddContent(OpenFieldContentType.WildSquad, cell, 0, squadId, DungeonTreasureQuality.Copper);
                return true;
            }

            return false;
        }

        private static bool IsAvailable(OpenFieldDungeonLayout l, OpenFieldGridPosition cell)
        {
            if (!l.IsWalkable(cell.X, cell.Y) || !l.IsReachable(cell.X, cell.Y)) return false;
            foreach (OpenFieldContentPlacement placement in l.ContentPlacements) if (placement.Cell.X == cell.X && placement.Cell.Y == cell.Y) return false;
            return true;
        }

        private static bool IsInsideInterestPoint(OpenFieldDungeonLayout l, OpenFieldGridPosition cell)
        {
            foreach (OpenFieldInterestPoint point in l.InterestPoints) { int x = cell.X - point.Center.X, y = cell.Y - point.Center.Y; if (x * x + y * y <= point.Radius * point.Radius) return true; }
            return false;
        }
    }
}
