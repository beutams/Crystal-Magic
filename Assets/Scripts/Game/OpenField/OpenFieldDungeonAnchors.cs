using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.OpenField
{
    public readonly struct OpenFieldGridPosition
    {
        public OpenFieldGridPosition(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
    }

    public enum OpenFieldInterestSize : byte { Small, Medium, Large }

    public sealed class OpenFieldInterestPoint
    {
        internal OpenFieldInterestPoint(int encounterId, OpenFieldInterestSize size, OpenFieldGridPosition center, int radius)
        { EncounterId = encounterId; Size = size; Center = center; Radius = radius; }
        public int EncounterId { get; }
        public OpenFieldInterestSize Size { get; }
        public OpenFieldGridPosition Center { get; }
        public int Radius { get; }
        public bool IsExitInterestPoint { get; internal set; }
    }

    [Serializable]
    public sealed class OpenFieldDungeonAnchorConfig
    {
        public int EntranceRadius = 4;
        public int SmallRadius = 3;
        public int MediumRadius = 5;
        public int LargeRadius = 7;
        public int BorderPadding = 7;
        public int PointGap = 4;
        public int PlacementAttempts = 512;
        internal void EnsureValid()
        {
            EntranceRadius = Mathf.Max(1, EntranceRadius); SmallRadius = Mathf.Max(1, SmallRadius);
            MediumRadius = Mathf.Max(SmallRadius, MediumRadius); LargeRadius = Mathf.Max(MediumRadius, LargeRadius);
            BorderPadding = Mathf.Max(1, BorderPadding); PointGap = Mathf.Max(0, PointGap); PlacementAttempts = Mathf.Max(1, PlacementAttempts);
        }
    }

    public static class OpenFieldDungeonAnchorGenerator
    {
        public static bool TryPlace(OpenFieldDungeonLayout layout, int seed, OpenFieldDungeonAnchorConfig config)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.EnsureValid(); layout.ClearAnchors();
            System.Random random = new(seed ^ 0x51ED270B);
            if (!TryPlace(layout, random, config.EntranceRadius, config.BorderPadding, config.BorderPadding, layout.Width / 3, config, out OpenFieldGridPosition entrance)) return false;
            layout.SetEntrance(entrance, config.EntranceRadius);
            int area = layout.CellCount;
            if (!PlaceGroup(layout, random, OpenFieldInterestSize.Large, config.LargeRadius, Mathf.Clamp(area / 7000, 1, 3), config) ||
                !PlaceGroup(layout, random, OpenFieldInterestSize.Medium, config.MediumRadius, Mathf.Clamp(area / 3500, 2, 6), config) ||
                !PlaceGroup(layout, random, OpenFieldInterestSize.Small, config.SmallRadius, Mathf.Clamp(area / 1600, 4, 12), config)) { layout.ClearAnchors(); return false; }
            List<OpenFieldInterestPoint> large = layout.GetPoints(OpenFieldInterestSize.Large);
            if (large.Count == 0) { layout.ClearAnchors(); return false; }
            layout.SetExitInterestPoint(large[random.Next(large.Count)]);
            return ValidateReachability(layout);
        }

        public static bool ValidateReachability(OpenFieldDungeonLayout layout)
        {
            if (!layout.HasEntrance || layout.ExitInterestPoint == null || !layout.IsWalkable(layout.Entrance.X, layout.Entrance.Y)) return false;
            layout.ClearReachability(); Queue<OpenFieldGridPosition> queue = new(); queue.Enqueue(layout.Entrance); layout.MarkReachable(layout.Entrance.X, layout.Entrance.Y);
            while (queue.Count > 0) { OpenFieldGridPosition p = queue.Dequeue(); Visit(layout, p.X - 1, p.Y, queue); Visit(layout, p.X + 1, p.Y, queue); Visit(layout, p.X, p.Y - 1, queue); Visit(layout, p.X, p.Y + 1, queue); }
            foreach (OpenFieldInterestPoint point in layout.InterestPoints) if (!layout.IsReachable(point.Center.X, point.Center.Y)) return false;
            return true;
        }

        private static bool PlaceGroup(OpenFieldDungeonLayout l, System.Random r, OpenFieldInterestSize s, int radius, int count, OpenFieldDungeonAnchorConfig c)
        { for (int i = 0; i < count; i++) { if (!TryPlace(l, r, radius, radius + 3, 0, l.Width, c, out OpenFieldGridPosition p)) return false; l.AddInterestPoint(s, p, radius); } return true; }
        private static bool TryPlace(OpenFieldDungeonLayout l, System.Random r, int radius, int border, int minX, int maxX, OpenFieldDungeonAnchorConfig c, out OpenFieldGridPosition result)
        {
            minX = Mathf.Max(border, minX); maxX = Mathf.Min(l.Width - border, maxX); int maxY = l.Height - border;
            for (int i = 0; i < c.PlacementAttempts && minX < maxX && border < maxY; i++) { OpenFieldGridPosition p = new(r.Next(minX, maxX), r.Next(border, maxY)); if (CanPlace(l, p, radius, c.PointGap)) { result = p; return true; } }
            result = default; return false;
        }
        private static bool CanPlace(OpenFieldDungeonLayout l, OpenFieldGridPosition p, int radius, int gap)
        {
            for (int y = p.Y - radius; y <= p.Y + radius; y++) for (int x = p.X - radius; x <= p.X + radius; x++) { int dx = x - p.X, dy = y - p.Y; if (dx * dx + dy * dy <= radius * radius && !l.IsWalkable(x, y)) return false; }
            if (l.HasEntrance && TooClose(p, radius, l.Entrance, l.EntranceRadius, gap)) return false;
            foreach (OpenFieldInterestPoint q in l.InterestPoints) if (TooClose(p, radius, q.Center, q.Radius, gap)) return false;
            return true;
        }
        private static bool TooClose(OpenFieldGridPosition a, int ar, OpenFieldGridPosition b, int br, int gap) { int x = a.X - b.X, y = a.Y - b.Y, d = ar + br + gap; return x * x + y * y < d * d; }
        private static void Visit(OpenFieldDungeonLayout l, int x, int y, Queue<OpenFieldGridPosition> q) { if (l.IsWalkable(x, y) && !l.IsReachable(x, y)) { l.MarkReachable(x, y); q.Enqueue(new OpenFieldGridPosition(x, y)); } }
    }
}