using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.OpenField
{
    public enum OpenFieldContentType : byte { Chest, InterestSquad, WildSquad }

    public sealed class OpenFieldContentPlacement
    {
        internal OpenFieldContentPlacement(OpenFieldContentType type, OpenFieldGridPosition cell, int encounterId, int squadId)
        { Type = type; Cell = cell; EncounterId = encounterId; SquadId = squadId; }
        public OpenFieldContentType Type { get; }
        public OpenFieldGridPosition Cell { get; }
        public int EncounterId { get; }
        public int SquadId { get; }
    }

    [Serializable]
    public sealed class OpenFieldDungeonContentConfig
    {
        public Vector3Int ChestCounts = new(1, 2, 3);
        public int WildSquadCount = 22;
        public int PlacementAttempts = 512;
        internal void EnsureValid() { ChestCounts = Vector3Int.Max(Vector3Int.zero, ChestCounts); WildSquadCount = Mathf.Max(0, WildSquadCount); PlacementAttempts = Mathf.Max(1, PlacementAttempts); }
        internal int GetChestCount(OpenFieldInterestSize size) => size switch { OpenFieldInterestSize.Small => ChestCounts.x, OpenFieldInterestSize.Medium => ChestCounts.y, _ => ChestCounts.z };
    }

    public static class OpenFieldDungeonContentGenerator
    {
        public static bool TryPlace(OpenFieldDungeonLayout layout, int seed, OpenFieldDungeonContentConfig config)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.EnsureValid(); layout.ClearContent(); System.Random random = new(seed ^ 0x2D9973A1); int squadId = 1;
            foreach (OpenFieldInterestPoint point in layout.InterestPoints)
            {
                if (!TryPlaceInsidePoint(layout, random, point, OpenFieldContentType.InterestSquad, point.EncounterId, squadId++, config.PlacementAttempts)) { layout.ClearContent(); return false; }
                for (int i = 0; i < config.GetChestCount(point.Size); i++)
                    if (!TryPlaceInsidePoint(layout, random, point, OpenFieldContentType.Chest, point.EncounterId, 0, config.PlacementAttempts)) { layout.ClearContent(); return false; }
            }
            for (int i = 0; i < config.WildSquadCount; i++)
                if (!TryPlaceWild(layout, random, squadId++, config.PlacementAttempts)) { layout.ClearContent(); return false; }
            return true;
        }

        private static bool TryPlaceInsidePoint(OpenFieldDungeonLayout l, System.Random r, OpenFieldInterestPoint p, OpenFieldContentType type, int encounterId, int squadId, int attempts)
        {
            int radius = Mathf.Max(1, p.Radius - 1), squared = radius * radius;
            for (int i = 0; i < attempts; i++)
            {
                OpenFieldGridPosition cell = new(r.Next(p.Center.X - radius, p.Center.X + radius + 1), r.Next(p.Center.Y - radius, p.Center.Y + radius + 1));
                int x = cell.X - p.Center.X, y = cell.Y - p.Center.Y;
                if (x * x + y * y <= squared && IsAvailable(l, cell)) { l.AddContent(type, cell, encounterId, squadId); return true; }
            }
            return false;
        }

        private static bool TryPlaceWild(OpenFieldDungeonLayout l, System.Random r, int squadId, int attempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                OpenFieldGridPosition cell = new(r.Next(0, l.Width), r.Next(0, l.Height));
                if (!l.IsReachable(cell.X, cell.Y) || IsInsideInterestPoint(l, cell) || !IsAvailable(l, cell)) continue;
                l.AddContent(OpenFieldContentType.WildSquad, cell, 0, squadId); return true;
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